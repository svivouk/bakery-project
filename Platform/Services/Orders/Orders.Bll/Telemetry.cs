using System.Diagnostics;

namespace Orders.Bll;

public static class Telemetry
{
    public const string SourceName = "Orders.Api";

    public static readonly ActivitySource Source = new(SourceName);
}