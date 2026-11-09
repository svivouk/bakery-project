namespace Catalog.Bll.Caching;

public static class CacheCallContext
{
    private static readonly AsyncLocal<CacheCallTracker?> CurrentTracker = new();

    public static CacheCallTracker? Current => CurrentTracker.Value;

    public static string Protocol => CurrentTracker.Value?.Protocol ?? "http";

    public static CacheCallTracker Begin(string protocol)
    {
        var tracker = new CacheCallTracker(protocol);
        CurrentTracker.Value = tracker;
        return tracker;
    }
}

public sealed class CacheCallTracker(string protocol)
{
    private int _hits;
    private int _misses;

    public string Protocol { get; } = protocol;
    public int Hits => Volatile.Read(ref _hits);
    public int Misses => Volatile.Read(ref _misses);

    internal void Hit() => Interlocked.Increment(ref _hits);
    internal void Miss() => Interlocked.Increment(ref _misses);
    public string Result => Misses > 0 ? "miss" : Hits > 0 ? "hit" : "none";
}
