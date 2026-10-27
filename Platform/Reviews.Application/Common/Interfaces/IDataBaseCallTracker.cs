namespace Reviews.Application.Common.Interfaces;
public interface IDatabaseCallTracker
{
    IDatabaseCallScope BeginScope();
}

public interface IDatabaseCallScope : IDisposable
{
    int CommandCount { get; }
    TimeSpan TotalDuration { get; }
    string? SlowestCommand { get; }
    TimeSpan SlowestDuration { get; }
}