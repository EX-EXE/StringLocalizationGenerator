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

    private static readonly string prefixName = "StringLocalization";
    private static readonly string generatorClassName = $"{prefixName}Generator";

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
            .Where(x => x.Item2 != null);
        context.RegisterSourceOutput(additionalTextsProvider, GenerateSource);
    }

    public static void GenerateSource(
        SourceProductionContext context,
       (AdditionalText, Microsoft.CodeAnalysis.Text.SourceText?) action)
    {
        if (action.Item2 != null)
        {
            var sourceGenerator = new JsonSourceGenerator(action.Item1, action.Item2);
            var sourceContext = new ThreadSafeSourceContext(context);
            sourceGenerator.Generate(sourceContext);
        }
    }
}