using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace StringLocalizationGenerator;

public partial class StringLocalizationMultiValueConverter : IMultiValueConverter
{
    public StringLocalizationMultiValueConverter()
    {
    }

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        foreach (var value in values.Reverse())
        {
            if (value == null)
            {
                continue;
            }
            if(value is string str)
            {
                return str;
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

        if (Key != null)
        {
            var property = StringLocalizationManager.CreateProperty((StringLocalizationKeyType)Key);
            multiBinding.Bindings.Add(new Binding()
            {
                Source = property,
                Path = nameof(StringLocalizationProperty.Value)
            });
        }

        if (KeyBinding != null)
        {
            multiBinding.Bindings.Add(KeyBinding);
        }
        return multiBinding;
    }
}
