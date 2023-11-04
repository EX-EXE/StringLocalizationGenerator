using System;
using System.Collections.Generic;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace StringLocalizationGenerator;

public static class JsonParser
{
    public static IJsonData Parse(ReadOnlySpan<char> text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Search Token
        var jsonTokens = new Queue<JsonToken>();
        bool isString = false;
        for (int index = 0; index < text.Length; ++index)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chr = text[index];

            if (isString)
            {
                if (chr == '"' && (0 < index && text[index - 1] != '\\'))
                {
                    isString = false;
                    jsonTokens.Enqueue(new JsonToken()
                    {
                        TokenType = JsonTokenType.DoubleQuotation,
                        Index = index,
                    });
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
            jsonTokens.Enqueue(new JsonToken()
            {
                TokenType = tokenType,
                Index = index,
            });
        }
        return ParseTokens(jsonTokens, text, cancellationToken);
    }

    private static IJsonData ParseTokens(Queue<JsonToken> queue, ReadOnlySpan<char> text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var currentToken = queue.Dequeue();
        if (currentToken.TokenType == JsonTokenType.DoubleQuotation)
        {
            // String
            var closeToken = queue.Dequeue();
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
#if DEBUG
                DebugString = text.Slice(start, length).ToString()
#endif
            };
        }
        else if (currentToken.TokenType == JsonTokenType.ObjectStart)
        {
            // Object
            var objectList = new List<(JsonString, IJsonData)>();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var peek = queue.Peek();
                if (peek.TokenType == JsonTokenType.Comma)
                {
                    queue.Dequeue();
                    continue;
                }
                if (peek.TokenType == JsonTokenType.ObjectEnd)
                {
                    queue.Dequeue();
                    break;
                }
                if (peek.TokenType != JsonTokenType.DoubleQuotation)
                {
                    throw new InvalidOperationException($"ObjectKey not found. Index({currentToken.Index})");
                }

                // 「ObjectKey : ObjectValue」
                var key = ParseTokens(queue, text, cancellationToken);
                if (key is not JsonString jsonStr)
                {
                    throw new InvalidOperationException($"ObjectKey is not string. Index({currentToken.Index})");
                }
                var coron = queue.Dequeue();
                if (coron.TokenType != JsonTokenType.Coron)
                {
                    throw new InvalidOperationException($"ObjectCoron not found. Index({currentToken.Index})");
                }
                var value = ParseTokens(queue, text, cancellationToken);
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
                var peek = queue.Peek();
                if (peek.TokenType == JsonTokenType.Comma)
                {
                    queue.Dequeue();
                    continue;
                }
                if (peek.TokenType == JsonTokenType.ArrayEnd)
                {
                    queue.Dequeue();
                    break;
                }

                var value = ParseTokens(queue, text, cancellationToken);
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

#if DEBUG
    public string DebugString { get; set; } = string.Empty;
#endif

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
