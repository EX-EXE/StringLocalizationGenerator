using Microsoft.CodeAnalysis.Text;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace StringLocalizationGenerator;

public static class JsonParser
{
    public static IJsonData Parse(Microsoft.CodeAnalysis.Text.SourceText sourceText, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tokens = ParseTokens(sourceText, cancellationToken);
        var queue = tokens.GetEnumerator();
        queue.MoveNext();
        return Convert(queue, cancellationToken);
    }

    private static IEnumerable<JsonToken> ParseTokens(Microsoft.CodeAnalysis.Text.SourceText sourceText, CancellationToken cancellationToken)
    {
        var bufferSize = 1024 * 1024 * 10;
        var buffer = ArrayPool<char>.Shared.Rent(bufferSize);
        try
        {
            var length = sourceText.Length;
            int position = 0;

            bool isString = false;
            char chrCache = '\0';
            while (position < length)
            {
                var currentPosition = position;
                int dstSize = Math.Min(length - position, bufferSize);
                sourceText.CopyTo(position, buffer, 0, dstSize);
                position += dstSize;

                for (int index = 0; index < dstSize; ++index)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var chr = buffer[index];
                    var beforeChar = chrCache;
                    chrCache = chr;

                    if (isString)
                    {
                        if (chr == '"' && beforeChar != '\\')
                        {
                            isString = false;
                            yield return new JsonToken()
                            {
                                TokenType = JsonTokenType.DoubleQuotation,
                                Index = currentPosition + index,
                            };
                        }
                        continue;
                    }

                    var tokenType = JsonTokenType.ObjectStart;
                    switch (chr)
                    {
                        case '{':
                            tokenType = JsonTokenType.ObjectStart;
                            break;
                        case '}':
                            tokenType = JsonTokenType.ObjectEnd;
                            break;
                        case '[':
                            tokenType = JsonTokenType.ArrayStart;
                            break;
                        case ']':
                            tokenType = JsonTokenType.ArrayEnd;
                            break;
                        case '"':
                            tokenType = JsonTokenType.DoubleQuotation;
                            isString = true;
                            break;
                        case ':':
                            tokenType = JsonTokenType.Coron;
                            break;
                        case ',':
                            tokenType = JsonTokenType.Comma;
                            break;
                        default:
                            continue;
                    }
                    yield return new JsonToken()
                    {
                        TokenType = tokenType,
                        Index = currentPosition + index,
                    };
                }
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }

    }

    private static IJsonData Convert(IEnumerator<JsonToken> queue, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var currentToken = queue.Current;
        queue.MoveNext();
        if (currentToken.TokenType == JsonTokenType.DoubleQuotation)
        {
            // String
            var closeToken = queue.Current;
            queue.MoveNext();
            if (closeToken.TokenType != JsonTokenType.DoubleQuotation)
            {
                throw new InvalidOperationException($"String not closed. Start({currentToken.Index}) Current({closeToken.Index})");
            }
            var start = currentToken.Index + 1;
            var length = closeToken.Index - currentToken.Index - 1;
            return new JsonString()
            {
                Start = start,
                Length = length,
            };
        }
        else if (currentToken.TokenType == JsonTokenType.ObjectStart)
        {
            // Object
            var objectList = new List<(JsonString, IJsonData)>();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var peek = queue.Current;
                if (peek.TokenType == JsonTokenType.Comma)
                {
                    queue.MoveNext();
                    continue;
                }
                if (peek.TokenType == JsonTokenType.ObjectEnd)
                {
                    queue.MoveNext();
                    break;
                }
                if (peek.TokenType != JsonTokenType.DoubleQuotation)
                {
                    throw new InvalidOperationException($"ObjectKey not found. Index({currentToken.Index})");
                }

                // 「ObjectKey : ObjectValue」
                var key = Convert(queue, cancellationToken);
                if (key is not JsonString jsonStr)
                {
                    throw new InvalidOperationException($"ObjectKey is not string. Index({currentToken.Index})");
                }
                var coron = queue.Current;
                queue.MoveNext();
                if (coron.TokenType != JsonTokenType.Coron)
                {
                    throw new InvalidOperationException($"ObjectCoron not found. Index({currentToken.Index})");
                }
                var value = Convert(queue, cancellationToken);
                objectList.Add((jsonStr, value));
            }
            return new JsonObject()
            {
                Objects = objectList.ToArray(),
            };
        }
        else if (currentToken.TokenType == JsonTokenType.ArrayStart)
        {
            // Array
            var objectList = new List<IJsonData>();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var peek = queue.Current;
                if (peek.TokenType == JsonTokenType.Comma)
                {
                    queue.MoveNext();
                    continue;
                }
                if (peek.TokenType == JsonTokenType.ArrayEnd)
                {
                    queue.MoveNext();
                    break;
                }

                var value = Convert(queue, cancellationToken);
                objectList.Add(value);
            }
            return new JsonArray()
            {
                Objects = objectList.ToArray(),
            };
        }
        else if (currentToken.TokenType == JsonTokenType.Comma)
        {
            // Comma
            return new JsonEmpty()
            {
            };
        }
        else
        {
            throw new InvalidOperationException($"Invalid Token. Index({currentToken.Index})");
        }
    }
}


public interface IJsonData
{
}

public class JsonEmpty : IJsonData
{
}

public class JsonString : IJsonData
{
    public int Start { get; set; } = -1;
    public int Length { get; set; } = -1;

    public string ToString(Microsoft.CodeAnalysis.Text.SourceText sourceText)
    {
        var buffer = ArrayPool<char>.Shared.Rent(Length);
        try
        {
            sourceText.CopyTo(Start, buffer, 0, Length);
            return buffer.AsSpan(0, Length).ToString();
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }
}

public class JsonObject : IJsonData
{
    public (JsonString, IJsonData)[] Objects { get; set; } = Array.Empty<(JsonString, IJsonData)>();
}

public class JsonArray : IJsonData
{
    public IJsonData[] Objects { get; set; } = Array.Empty<IJsonData>();
}

public enum JsonTokenType : uint
{
    /// <summary>
    /// {
    /// </summary>
    ObjectStart,
    /// <summary>
    /// }
    /// </summary>
    ObjectEnd,

    /// <summary>
    /// [
    /// </summary>
    ArrayStart,
    /// <summary>
    /// ]
    /// </summary>
    ArrayEnd,

    /// <summary>
    /// "
    /// </summary>
    DoubleQuotation,
    /// <summary>
    /// :
    /// </summary>
    Coron,
    /// <summary>
    /// ,
    /// </summary>
    Comma,
}
public struct JsonToken
{
    public JsonTokenType TokenType { get; set; } = JsonTokenType.ObjectStart;
    public int Index { get; set; } = -1;

    public JsonToken()
    {
    }
}
