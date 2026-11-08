using System.Diagnostics;

namespace Reviews.Application;

public static class Telemetry
{
    public const string SourceName = "Reviews.Api";

    public static readonly ActivitySource Source = new(SourceName);
}