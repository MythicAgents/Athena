namespace screenshot;

public static class DisposableCollector
{
    public static List<TDisposable> Collect<TSource, TDisposable>(
        IEnumerable<TSource> sources,
        Func<TSource, TDisposable> capture)
        where TDisposable : IDisposable
    {
        var captured = new List<TDisposable>();
        try
        {
            foreach (var source in sources)
            {
                captured.Add(capture(source));
            }

            return captured;
        }
        catch
        {
            foreach (var resource in captured)
            {
                resource.Dispose();
            }

            throw;
        }
    }
}
