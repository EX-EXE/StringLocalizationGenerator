using Microsoft.CodeAnalysis;

namespace StringLocalizationGenerator;

public class ThreadSafeSourceContext
{
    private readonly object lockObj = new object();
    private readonly SourceProductionContext context;

    public CancellationToken CancellationToken => context.CancellationToken;

    public ThreadSafeSourceContext(SourceProductionContext context)
    {
        this.context = context;
    }

    public void AddSource(ReadOnlySpan<char> hints, ReadOnlySpan<char> source)
    {
        lock (lockObj)
        {
            context.AddSource(hints.ToString(), source.ToString());
        }
    }
}