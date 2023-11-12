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
    private int keyId = 0;


    private readonly object languageLock = new object();
    private int languageId = 0;
    private readonly Dictionary<string, int> languageDict = new();


    private int GetOrAddLanguageId(ReadOnlySpan<char> language)
    {
        var key = language.Trim().ToString().ToUpperInvariant();

        if (languageDict.TryGetValue(key, out var value))
        {
            return value;
        }

        lock (languageLock)
        {
            if (languageDict.TryGetValue(key, out var lockValue))
            {
                return lockValue;
            }

            var result = languageId;
            languageDict.Add(key, result);
            ++languageId;
            return result;
        }
    }

    public JsonSourceGenerator(AdditionalText additionalText, SourceText sourceText)
    {
        this.additionalText = additionalText;
        this.sourceText = sourceText;
        GetOrAddLanguageId("Default".AsSpan());
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
        var outputEnumLanguageName = $"{fileName}LanguageType";
        var outputPropertyName = $"{fileName}Property";

        var keyDict = new Dictionary<string, int>();

        var languageList = new List<(ulong id, ulong index, int length)>();
        var binaryList = new List<string>();
        var binaryIndex = 0UL;
        foreach (var json in JsonParser.Parse(sourceText, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var jsonStr = json.Item1;
            var jsonData = json.Item2;

            if (jsonData is JsonObject jsonObject)
            {
                // Key
                var textKeyStr = jsonStr.ToString(sourceText);
                var textKeyId = keyId;
                Interlocked.Increment(ref keyId);
                keyDict.Add(textKeyStr, textKeyId);

                // Language
                foreach (var (languageKey, languageObj) in jsonObject.Objects)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (languageObj is JsonString languageString)
                    {
                        var languageName = languageKey.ToString(sourceText);
                        var languageData = languageString.ToString(sourceText);

                        var languageId = GetOrAddLanguageId(languageName.AsSpan());
                        var languageBytes = System.Text.Encoding.UTF8.GetBytes(languageData);

                        var id = (Convert.ToUInt64(textKeyId) << 32) + Convert.ToUInt64(languageId);
                        languageList.Add((id, binaryIndex, languageBytes.Length));

                        binaryList.Add($"{string.Concat(languageBytes.Select(x => $"0x{x.ToString("X2")}, "))} // Index({binaryIndex}) Length({languageBytes.Length}) Id({id}) {textKeyStr}({languageData})[{languageName}] ");
                        binaryIndex += Convert.ToUInt64(languageBytes.Length);
                    }
                }

            }
        }


        sourceContext.AddSource($"{outputClassName}.g.cs".AsSpan(), $$"""
using System;
using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StringLocalizationGenerator;


public enum {{outputEnumKeyName}} : int
{
{{string.Join("\n", keyDict.Select(x => $"\t{x.Key} = {x.Value},"))}}
}

public enum {{outputEnumLanguageName}} : int
{
{{string.Join("\n", languageDict.Select(x => $"\t{x.Key} = {x.Value},"))}}
}

public class {{outputPropertyName}} : INotifyPropertyChanged, IDisposable
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private {{outputEnumKeyName}} key;
    public {{outputEnumKeyName}} Key
    {
        get
        {
            return key;
        }
        set
        {
            if (key != value)
            {
                this.key = value;
                NotifyPropertyChanged();
            }
        }
    }

    public string Value
        => {{outputClassName}}.GetString(Key);

    internal {{outputPropertyName}}({{outputEnumKeyName}} keyType)
    {
        Key = keyType;
    }

    public void Dispose()
    {
        {{outputClassName}}.RemoveProperty(this);
    }

    public void NotifyPropertyChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Value"));
    }

}


public class {{outputClassName}} : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private static readonly Lazy<{{{outputClassName}}}> instance = new Lazy<{{{outputClassName}}}>(() => new {{{outputClassName}}}());
    public static {{{outputClassName}}} Shared => instance.Value;

    private static {{outputEnumLanguageName}} currentLanguage = {{outputEnumLanguageName}}.DEFAULT;
    public {{outputEnumLanguageName}} CurrentLanguage => currentLanguage;

    private static ReadOnlySpan<byte> binary => [
{{string.Join("\n", binaryList.Select(x => $"\t\t{x}"))}}
    ];

    private static object propertyListLock = new object();
    private static List<{{outputPropertyName}}> propertyList = new();
    public static {{outputPropertyName}} CreateProperty({{outputEnumKeyName}} keyType)
    {
        var instance = new {{outputPropertyName}}(keyType);
        lock(propertyListLock)
        {
            propertyList.Add(instance);
        }
        return instance;
    }
    public static void RemoveProperty({{outputPropertyName}} instance)
    {
        lock(propertyListLock)
        {
            if(propertyList.Contains(instance))
            {
                propertyList.Remove(instance);
            }
        }
    }
    public static void NotifyProperties()
    {
        lock(propertyListLock)
        {
            foreach(var property in propertyList)
            {
                property.NotifyPropertyChanged();
            }
        }
    }

    public static void ChangeLanguage({{outputEnumLanguageName}} languageType)
    {
        if(currentLanguage != languageType)
        {
            currentLanguage = languageType;
            Shared.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
            NotifyProperties();
        }
    }

    public static string GetString({{outputEnumKeyName}} keyType)
    {
        return System.Text.Encoding.UTF8.GetString(GetBytes(keyType, currentLanguage));
    }

    private static ReadOnlySpan<byte> GetBytes({{outputEnumKeyName}} keyType, {{outputEnumLanguageName}} languageType)
    {
        (int index,int length) = GetIndexAndLength(keyType, languageType);
        return binary.Slice(index, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (int index,int length) GetIndexAndLength({{outputEnumKeyName}} keyType, {{outputEnumLanguageName}} languageType)
    {
        var id = (Convert.ToUInt64(keyType) << 32) + Convert.ToUInt64(languageType);
        return id switch
        {
{{string.Join("\n", languageList.Select(x => $"\t\t\t{x.id}UL => ({x.index}, {x.length}),"))}}
            _ => throw new System.InvalidOperationException($"NotFound Localization.")
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
    public Binding? KeyBinding { get; set; } = null;


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
    public CompiledBindingExtension? KeyBinding { get; set; } = null;

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
