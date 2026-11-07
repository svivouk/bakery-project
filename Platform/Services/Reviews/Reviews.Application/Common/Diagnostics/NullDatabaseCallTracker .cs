using Reviews.Application.Common.Interfaces;

namespace Reviews.Application.Common.Diagnostics;


public sealed class NullDatabaseCallTracker : IDatabaseCallTracker
{
    public IDatabaseCallScope BeginScope() => EmptyScope.Instance;

    private sealed class EmptyScope : IDatabaseCallScope
    {
        public static readonly EmptyScope Instance = new();
        public int CommandCount => 0;
        public TimeSpan TotalDuration => TimeSpan.Zero;
        public string? SlowestCommand => null;
        public TimeSpan SlowestDuration => TimeSpan.Zero;
        public void Dispose() { }
    }
}