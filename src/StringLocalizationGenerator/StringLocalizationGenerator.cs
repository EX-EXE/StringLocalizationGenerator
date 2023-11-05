using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Buffers;
using System.Collections.Immutable;
using System.Text;

namespace StringLocalizationGenerator;

public class GeneratorBuildPropertyInfo
{
    public bool OutputWpfMarkupExtension { get; set; } = false;
    public bool OutputAvaloniaMarkupExtension { get; set; } = false;
}

[Generator(LanguageNames.CSharp)]
public partial class StringLocalizationGenerator : IIncrementalGenerator
{
    private static readonly string jsonFileName = @"StringLocalization.json";

    private static readonly string defaultName = @"default";

    private static readonly string prefixName = "StringLocalization";
    private static readonly string generatorClassName = $"{prefixName}Generator";
    private static readonly string converterClassName = $"{prefixName}Converter";
    private static readonly string managerClassName = $"{prefixName}Manager";
    private static readonly string keyTypeEnumName = $"{prefixName}KeyType";
    private static readonly string markupClassName = $"BindingExtension";

    private static readonly string namespaceName = generatorClassName;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var additionalTextsProvider = context.AdditionalTextsProvider
            .Select((additionalText, cancellationToken) =>
            {
                if (additionalText.Path.EndsWith(jsonFileName, StringComparison.OrdinalIgnoreCase))
                {
                    return (additionalText, additionalText.GetText(cancellationToken));
                }
                return (additionalText, null);
            })
            .Where(x => x.Item2 != null)
            .Collect();

        var analyzerConfigOptionsProvider = context.AnalyzerConfigOptionsProvider
            .Select((configOptions, token) =>
            {
                bool IsEnable(ReadOnlySpan<char> data)
                {
                    var trim = data.Trim();
                    if (trim.Equals("1".AsSpan(), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    if (trim.Equals("yes".AsSpan(), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    if (trim.Equals("enable".AsSpan(), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    return false;
                }
                bool ParseEnable(string propertyName, AnalyzerConfigOptionsProvider provider)
                {
                    if (provider.GlobalOptions.TryGetValue(
                        $"build_property.{generatorClassName}_{propertyName}",
                        out var value))
                    {
                        return IsEnable(value.AsSpan());
                    }
                    return false;
                }

                var info = new GeneratorBuildPropertyInfo();
                info.OutputWpfMarkupExtension = ParseEnable(nameof(GeneratorBuildPropertyInfo.OutputWpfMarkupExtension), configOptions);
                info.OutputAvaloniaMarkupExtension = ParseEnable(nameof(GeneratorBuildPropertyInfo.OutputAvaloniaMarkupExtension), configOptions);
                return info;
            });

        var source = additionalTextsProvider.Combine(analyzerConfigOptionsProvider);
        context.RegisterSourceOutput(source, GenerateSource);
    }


    public class JsonSourceInfo
    {
        public JsonSourceInfo(Microsoft.CodeAnalysis.Text.SourceText sourceText)
        {
            this.SourceText = sourceText;
        }
        public string FileName { get; set; } = string.Empty;
        public Microsoft.CodeAnalysis.Text.SourceText SourceText { get; set; }
        public JsonObject JsonObject { get; set; } = new JsonObject();
    }

    public static void GenerateSource(
        SourceProductionContext context,
       (ImmutableArray<(AdditionalText, Microsoft.CodeAnalysis.Text.SourceText?)>, GeneratorBuildPropertyInfo) action)
    {
        var sourceTextArray = action.Item1;
        var buildProperty = action.Item2;

        GenerateSource(context, sourceTextArray);
        GenerateMarkupSource(context, buildProperty);
    }


    private static void GenerateSource(
        SourceProductionContext context,
        ImmutableArray<(AdditionalText, Microsoft.CodeAnalysis.Text.SourceText?)> sourceTextArray)

    {
        var cancellationToken = context.CancellationToken;
        // Json Read
        var jsonSourceList = new List<JsonSourceInfo>();
        foreach (var (additionalText, sourceText) in sourceTextArray)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sourceText == null)
            {
                continue;
            }

            // FileName
            var fileName = System.IO.Path.GetFileName(additionalText.Path);
            //var fileNameWithout = fileName.Substring(0, fileName.Length - jsonFileName.Length).Trim('.', ' ');

            // Json Parse
            var jsonRoot = JsonParser.Parse(sourceText, cancellationToken);

            // Json Object Only
            if (jsonRoot is JsonObject jsonObj)
            {
                jsonSourceList.Add(new JsonSourceInfo(sourceText)
                {
                    FileName = fileName,
                    JsonObject = jsonObj,
                });
            }
        }

        // Language
        var languageHash = new HashSet<string>();
        foreach (var jsonSource in jsonSourceList)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var (key, languages) in jsonSource.JsonObject.Objects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (languages is JsonObject jsonLanguage)
                {
                    foreach (var (languageKey, languageValue) in jsonLanguage.Objects)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var languageName = languageKey.ToString(jsonSource.SourceText).Trim().ToLowerInvariant();
                        if (!languageName.Equals(defaultName, StringComparison.OrdinalIgnoreCase))
                        {
                            languageHash.Add(languageName);
                        }
                    }
                }
            }
        }
        var launguageIndex = 0;
        var languageDict = new Dictionary<string, int>();
        languageDict[defaultName] = launguageIndex++;
        foreach (var languageName in languageHash.OrderBy(x => x))
        {
            languageDict[languageName] = launguageIndex++;
        }
        var languageSort = languageDict.OrderBy(x => x.Value).ToArray();

        // Source
        var keyNameIndex = 1;
        var keyNameList = new List<string>();
        var keyTypeSourceBuilder = new StringBuilder();
        foreach (var jsonSource in jsonSourceList)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var (keyObj, languagesObj) in jsonSource.JsonObject.Objects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var languageIndexData = new SortedList<int, (string, string)>();

                var defaultData = (string?)null;
                var keyName = keyObj.ToString(jsonSource.SourceText).Trim();

                if (languagesObj is JsonObject jsonLanguages)
                {
                    foreach (var (languageKey, languageValue) in jsonLanguages.Objects)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (languageValue is JsonString languageString)
                        {
                            var languageName = languageKey.ToString(jsonSource.SourceText).Trim().ToLowerInvariant();
                            var languageData = languageString.ToString(jsonSource.SourceText);
                            if (languageName.Equals(defaultName, StringComparison.OrdinalIgnoreCase))
                            {
                                defaultData = languageData;
                            }
                            else if (languageDict.TryGetValue(languageName, out var index))
                            {
                                languageIndexData.Add(index, (languageName, languageData));
                            }
                        }
                    }
                }

                var comments = new List<string>();
                comments.Add($$"""/// <summary>""");
                comments.Add($$"""/// {{keyName}}""");
                comments.Add($$"""/// <code>""");
                comments.AddRange(languageIndexData.Select(x => $$"""/// {{x.Value.Item1}} : {{x.Value.Item2}}"""));
                comments.Add($$"""/// {{(defaultData != null ? $"Default : {defaultData}" : string.Empty)}}""");
                comments.Add($$"""/// </code>""");
                comments.Add($$"""/// </summary>""");

                keyNameList.Add(keyName);

                keyTypeSourceBuilder.AppendLine($$"""
{{string.Join("\n", comments.Select(x => $"\t{x}"))}}
    {{keyName}} = {{keyNameIndex++}},
""");

                GenerateFieldSource(context, keyName, $$"""
{{string.Join("\n", comments.Select(x => $"\t{x}"))}}
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string STRING_{{keyName}}(int languageIndex)
        => languageIndex switch
        {
{{string.Join("\n", languageIndexData.Select(x => $"\t\t\t{x.Key} => \"{x.Value.Item2}\","))}}
            _ => {{(defaultData != null ? $"\"{defaultData}\"" : $"throw new System.InvalidOperationException($\"NotFound Localization. Key({keyName}) Language({{LanguageNames[languageIndex]}})\"),")}}
        };

{{string.Join("\n", comments.Select(x => $"\t{x}"))}}
    public static string STRING_{{keyName}}()
        => STRING_{{keyName}}(currentLanguageIndex);

{{string.Join("\n", comments.Select(x => $"\t{x}"))}}
    public string {{keyName}}
        => STRING_{{keyName}}(currentLanguageIndex);
""");
            }
        }

        // Create Source
        var outputFileName = $"{namespaceName}.{managerClassName}.cs";
        context.AddSource(outputFileName, $$$"""
using System;
using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace {{{namespaceName}}};

public enum {{{keyTypeEnumName}}} : ulong
{
{{{keyTypeSourceBuilder}}}
}

public partial class {{{managerClassName}}} : INotifyPropertyChanged 
{
    private static readonly Lazy<{{{managerClassName}}}> instance = new Lazy<{{{managerClassName}}}>(() => new {{{managerClassName}}}());
    public static {{{managerClassName}}} Shared => instance.Value;


    public static string[] KeyNames = {{{{string.Join(", ", keyNameList.Select(x => $"\"{x}\""))}}}};
    public static {{{keyTypeEnumName}}} GetKeyType(ReadOnlySpan<char> key)
    {
        return key switch
            {
{{{string.Join("\n", keyNameList.Select(x => $"\t\t\t\t\"{x}\" => {keyTypeEnumName}.{x},"))}}}
                _ => throw new System.NotImplementedException($"NotImplemented Key. ({key})")
            };
    }

    public static string[] LanguageNames = {{{{string.Join(", ", languageSort.Select(x => $"\"{x.Key}\""))}}}};
    private static int currentLanguageIndex = 0;
    public int CurrentLanguageIndex => currentLanguageIndex;

    public event PropertyChangedEventHandler? PropertyChanged;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetLanguageIndex(ReadOnlySpan<char> lang)
    {
        var buffer = ArrayPool<char>.Shared.Rent(lang.Length);
        try
        {
            var bufferSpan = buffer.AsSpan();
            var len = lang.ToLowerInvariant(bufferSpan);
            bufferSpan = bufferSpan.Slice(0, len);

            var index = bufferSpan switch
            {
{{{string.Join("\n", languageSort.Select(x => $"\t\t\t\t\"{x.Key}\" => {x.Value},"))}}}
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
    public static bool ChangeLanguage(int languageIndex)
    {
        if(languageIndex < 0 || LanguageNames.Length <= languageIndex)
        {
            return false;
        }
        if(currentLanguageIndex != languageIndex)
        {
            currentLanguageIndex = languageIndex;
            Shared.PropertyChanged?.Invoke(Shared, new PropertyChangedEventArgs("CurrentLanguageIndex"));
{{{string.Join("\n", keyNameList.Select(x => "\t\t\t" + $"""Shared.PropertyChanged?.Invoke(Shared, new PropertyChangedEventArgs("{x}"));"""))}}}
        }
        return true;
    }

    public static bool ChangeLanguage(ReadOnlySpan<char> lang)
    {
        var index = GetLanguageIndex(lang);
        return ChangeLanguage(index);
    }

    public static string GetString({{{keyTypeEnumName}}} type)
    {
        return type switch
        {
{{{string.Join("\n", keyNameList.Select(key => $"\t\t\t{keyTypeEnumName}.{key} => STRING_{key}(),"))}}}
            _ => throw new System.NotImplementedException($"NotImplemented Type. ({type})")
        };
    }
}
""");

    }

    private static void GenerateFieldSource(
        SourceProductionContext context,
        string key,
        string field)
    {
        var outputFileName = $"{namespaceName}.{managerClassName}.{key}.cs";
        context.AddSource(outputFileName, $$$"""
using System;
using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace {{{namespaceName}}};

partial class {{{managerClassName}}} 
{
{{{field}}}
}
""");
    }

    private static void GenerateMarkupSource(
        SourceProductionContext context,
        GeneratorBuildPropertyInfo propertyInfo)
    {
        if (propertyInfo.OutputWpfMarkupExtension)
        {
            var outputFileName = $"{namespaceName}.{markupClassName}.cs";
            context.AddSource(outputFileName, $$$"""
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace {{{namespaceName}}};

public partial class StringLocalizationMultiValueConverter : IMultiValueConverter
{
    public StringLocalizationMultiValueConverter()
    {
    }

    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
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
                return StringLocalizationManager.GetString((StringLocalizationKeyType)num);
            }
            catch (InvalidCastException)
            {
                continue;
            }
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public partial class BindingExtension : MarkupExtension
{
    public StringLocalizationKeyType? Key { get; set; } = null;
    public Binding? KeyBinding { get; set; } = null;


    public override object ProvideValue(System.IServiceProvider serviceProvider)
    {
        var multiBinding = new MultiBinding()
        {
            Converter = new StringLocalizationMultiValueConverter(),
            NotifyOnSourceUpdated = true,
        };
        multiBinding.Bindings.Add(new Binding()
        {
            Source = StringLocalizationManager.Shared,
            Path = new PropertyPath(nameof(StringLocalizationManager.CurrentLanguageIndex)),
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

""");
        }
        else if (propertyInfo.OutputAvaloniaMarkupExtension)
        {
            var outputFileName = $"{namespaceName}.{markupClassName}.cs";
            context.AddSource(outputFileName, $$$"""
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace {{{namespaceName}}};

public partial class StringLocalizationMultiValueConverter : IMultiValueConverter
{
    public StringLocalizationMultiValueConverter()
    {
    }

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
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
                return StringLocalizationManager.GetString((StringLocalizationKeyType)num);
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
    public StringLocalizationKeyType? Key { get; set; } = null;
    public CompiledBindingExtension? KeyBinding { get; set; } = null;

    public override object ProvideValue(System.IServiceProvider serviceProvider)
    {
        var multiBinding = new MultiBinding()
        {
            Converter = new StringLocalizationMultiValueConverter(),
        };
        multiBinding.Bindings.Add(new Binding()
        {
            Source = StringLocalizationManager.Shared,
            Path = nameof(StringLocalizationManager.CurrentLanguageIndex),
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
""");
        }
    }

}