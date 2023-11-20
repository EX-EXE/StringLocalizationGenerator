using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace StringLocalizationGenerator;

internal class JsonSourceGenerator
{
    private static readonly long intMax = Convert.ToInt64(int.MaxValue);

    private readonly AdditionalText additionalText;
    private readonly SourceText sourceText;
    private int id = 0;

    public JsonSourceGenerator(AdditionalText additionalText, SourceText sourceText)
    {
        this.additionalText = additionalText;
        this.sourceText = sourceText;
    }

    public void Generate(ThreadSafeSourceContext sourceContext)
    {
        var fileName = System.IO.Path.GetFileNameWithoutExtension(additionalText.Path);
        GenerateManager(fileName, sourceContext);
        GenerateMarkup(fileName, sourceContext);
    }

    public void GenerateManager(
        string fileName,
        ThreadSafeSourceContext sourceContext)
    {
        var cancellationToken = sourceContext.CancellationToken;
        // FileName
        //var fileNameWithout = fileName.Substring(0, fileName.Length - jsonName.Length);
        var outputClassName = $"{fileName}Manager";
        var outputEnumKeyName = $"{fileName}KeyType";

        var keyDict = new ConcurrentDictionary<string, int>();
        var languageDict = new ConcurrentDictionary<string, long>();

        Parallel.ForEach(JsonParser.Parse(sourceText, cancellationToken), json =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var jsonStr = json.Item1;
            var jsonData = json.Item2;

            if (jsonData is JsonObject jsonObject)
            {
                var defaultData = (string?)null;
                var languageList = new List<(long, string, string)>();
                foreach (var (languageKey, languageObj) in jsonObject.Objects)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (languageObj is JsonString languageString)
                    {
                        var languageName = languageKey.ToString(sourceText).Trim().ToLowerInvariant();
                        var languageData = languageString.ToString(sourceText);
                        if (languageName.Equals("default", StringComparison.OrdinalIgnoreCase))
                        {
                            defaultData = languageData;
                        }
                        else
                        {
                            var languageHash = Convert.ToInt64(languageName.GetHashCode()) + intMax + 1;
                            languageDict.TryAdd(languageName, languageHash);
                            languageList.Add((languageHash, languageData, languageName));
                        }
                    }
                }

                var jsonKey = jsonStr.ToString(sourceText);
                var keyHash = id;
                Interlocked.Increment(ref id);
                keyDict.TryAdd(jsonKey, keyHash);

                sourceContext.AddSource($"{outputClassName}.{jsonKey}.g.cs".AsSpan(), $$"""
using System;
using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StringLocalizationGenerator;

partial class {{outputClassName}}
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string STRING_{{jsonKey}}(long languageIndex)
        => languageIndex switch
        {
{{string.Join("\n", languageList.Select(x => $"\t\t\t{x.Item1} => \"{x.Item2}\", // {x.Item3}"))}}
            _ => {{(defaultData != null ? $"\"{defaultData}\"" : $"throw new System.InvalidOperationException($\"NotFound Localization. Key({jsonKey}) Language({{LanguageNames[languageIndex]}})\"),")}}
        };

    public static string STRING_{{jsonKey}}()
        => STRING_{{jsonKey}}(currentLanguageIndex);

    public string {{jsonKey}}
        => STRING_{{jsonKey}}(currentLanguageIndex);
}
""".AsSpan());
            }
        });

        // Create Source
        sourceContext.AddSource($"{outputClassName}.g.cs".AsSpan(), $$$"""
using System;
using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StringLocalizationGenerator;

public enum {{{outputEnumKeyName}}} : int
{
{{{string.Join("\n", keyDict.Select(x => $"\t{x.Key} = {x.Value},"))}}}
}

public partial class {{{outputClassName}}} : INotifyPropertyChanged 
{
    private static readonly Lazy<{{{outputClassName}}}> instance = new Lazy<{{{outputClassName}}}>(() => new {{{outputClassName}}}());
    public static {{{outputClassName}}} Shared => instance.Value;


    public static string[] KeyNames = {{{{string.Join(", ", keyDict.Select(x => $"\"{x.Key}\""))}}}};
    public static {{{outputEnumKeyName}}} GetKeyType(ReadOnlySpan<char> key)
    {
        return key switch
            {
{{{string.Join("\n", keyDict.Select(x => $"\t\t\t\t\"{x.Key}\" => {outputEnumKeyName}.{x.Key},"))}}}
                _ => throw new System.NotImplementedException($"NotImplemented Key. ({key})")
            };
    }

    public static string[] LanguageNames = {{{{string.Join(", ", languageDict.Select(x => $"\"{x.Key}\""))}}}};
    private static long currentLanguageIndex = -1;
    public long CurrentLanguageIndex => currentLanguageIndex;

    public event PropertyChangedEventHandler PropertyChanged;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GetLanguageIndex(ReadOnlySpan<char> lang)
    {
        var buffer = ArrayPool<char>.Shared.Rent(lang.Length);
        try
        {
            var bufferSpan = buffer.AsSpan();
            var len = lang.ToLowerInvariant(bufferSpan);
            bufferSpan = bufferSpan.Slice(0, len);

            var index = bufferSpan switch
            {
{{{string.Join("\n", languageDict.Select(x => $"\t\t\t\t\"{x.Key}\" => {x.Value}L,"))}}}
                _ => -1,
            };
            return index;
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ChangeLanguage(long languageIndex)
    {
        if(currentLanguageIndex != languageIndex)
        {
            currentLanguageIndex = languageIndex;
            Shared.PropertyChanged?.Invoke(Shared, new PropertyChangedEventArgs("CurrentLanguageIndex"));
{{{string.Join("\n", keyDict.Select(x => "\t\t\t" + $"""Shared.PropertyChanged?.Invoke(Shared, new PropertyChangedEventArgs("{x.Key}"));"""))}}}
        }
    }

    public static void ChangeDefaultLanguage()
    {
        ChangeLanguage(-1);
    }

    public static void ChangeLanguage(ReadOnlySpan<char> lang)
    {
        var index = GetLanguageIndex(lang);
        ChangeLanguage(index);
    }

    public static string GetString({{{outputEnumKeyName}}} type)
    {
        return type switch
        {
{{{string.Join("\n", keyDict.Select(x => $"\t\t\t{outputEnumKeyName}.{x.Key} => STRING_{x.Key}(),"))}}}
            _ => throw new System.NotImplementedException($"NotImplemented Type. ({type})")
        };
    }
}
""".AsSpan());
    }


    private static void GenerateMarkup(
        string fileName,
        ThreadSafeSourceContext context)
    {
        context.AddSource($"{fileName}.WPF.g.cs".AsSpan(), $$"""
#if WITH_STRING_LOCALIZATION_WPF_MARKUP
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace StringLocalizationGenerator;

public partial class {{fileName}}MultiValueConverter : IMultiValueConverter
{
    public {{fileName}}MultiValueConverter()
    {
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        foreach (var value in values.Skip(1).Reverse())
        {
            if (value == null || value == DependencyProperty.UnsetValue)
            {
                continue;
            }
            try
            {
                var num = System.Convert.ToUInt64(value);
                return {{fileName}}Manager.GetString(({{fileName}}KeyType)num);
            }
            catch (InvalidCastException)
            {
                continue;
            }
        }
        return null;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public partial class BindingExtension : MarkupExtension
{
    public {{fileName}}KeyType? Key { get; set; } = null;
    public Binding KeyBinding { get; set; } = null;


    public override object ProvideValue(System.IServiceProvider serviceProvider)
    {
        var multiBinding = new MultiBinding()
        {
            Converter = new {{fileName}}MultiValueConverter(),
            NotifyOnSourceUpdated = true,
        };
        multiBinding.Bindings.Add(new Binding()
        {
            Source = {{fileName}}Manager.Shared,
            Path = new PropertyPath(nameof({{fileName}}Manager.CurrentLanguageIndex)),
        });

        if (Key != null)
        {
            multiBinding.Bindings.Add(new Binding()
            {
                Source = Key,
            });
        }

        if (KeyBinding != null)
        {
            multiBinding.Bindings.Add(KeyBinding);
        }
        return multiBinding.ProvideValue(serviceProvider);
    }
}
#endif // WITH_STRING_LOCALIZATION_WPF_MARKUP
""".AsSpan());

        // Avalonia
        context.AddSource($"{fileName}.Avalonia.g.cs".AsSpan(), $$$"""
#if WITH_STRING_LOCALIZATION_AVALONIA_MARKUP
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace StringLocalizationGenerator;

public partial class {{{fileName}}}MultiValueConverter : IMultiValueConverter
{
    public {{{fileName}}}MultiValueConverter()
    {
    }

    public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
    {
        foreach (var value in values.Skip(1).Reverse())
        {
            if (value == null)
            {
                continue;
            }
            try
            {
                var num = System.Convert.ToUInt64(value);
                return {{{fileName}}}Manager.GetString(({{{fileName}}}KeyType)num);
            }
            catch (InvalidCastException)
            {
                continue;
            }
        }
        return null;
    }
}

public partial class BindingExtension : MarkupExtension
{
    public {{{fileName}}}KeyType? Key { get; set; } = null;
    public CompiledBindingExtension KeyBinding { get; set; } = null;

    public override object ProvideValue(System.IServiceProvider serviceProvider)
    {
        var multiBinding = new MultiBinding()
        {
            Converter = new {{{fileName}}}MultiValueConverter(),
        };
        multiBinding.Bindings.Add(new Binding()
        {
            Source = {{{fileName}}}Manager.Shared,
            Path = nameof({{{fileName}}}Manager.CurrentLanguageIndex),
        });

        if (Key != null)
        {
            multiBinding.Bindings.Add(new Binding()
            {
                Source = Key,
            });
        }

        if (KeyBinding != null)
        {
            multiBinding.Bindings.Add(KeyBinding);
        }
        return multiBinding;
    }
}
#endif // WITH_STRING_LOCALIZATION_AVALONIA_MARKUP
""".AsSpan());
    }
}
