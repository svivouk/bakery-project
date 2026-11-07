namespace Reviews.Application.Common.Behaviors;

public sealed class PerformanceOptions
{
    public TimeSpan SlowRequestThreshold { get; init; } = TimeSpan.FromMilliseconds(500);
}