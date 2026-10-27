using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Reviews.Application.Common.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Reviews.Infrastructure.Logging;
public sealed class SensitiveDataDestructuringPolicy : IDestructuringPolicy
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]?> Cache = new();

    public bool TryDestructure(object value, ILogEventPropertyValueFactory factory,
                               [NotNullWhen(true)] out LogEventPropertyValue? result)
    {
        var type = value.GetType();
        var properties = Cache.GetOrAdd(type, Inspect);
        if (properties is null)
        {
            result = null;
            return false;
        }

        var members = new List<LogEventProperty>(properties.Length);
        foreach (var property in properties)
        {
            LogEventPropertyValue propertyValue = property.IsDefined(typeof(SensitiveAttribute), inherit: true)
                ? new ScalarValue("***")
                : factory.CreatePropertyValue(property.GetValue(value), destructureObjects: true);

            members.Add(new LogEventProperty(property.Name, propertyValue));
        }

        result = new StructureValue(members, type.Name);
        return true;
    }

    private static PropertyInfo[]? Inspect(Type type)
    {
        if (type.IsPrimitive || type.IsEnum || type == typeof(string)) return null;

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToArray();

        return properties.Any(p => p.IsDefined(typeof(SensitiveAttribute), inherit: true)) ? properties : null;
    }
}

public static class SerilogConfigurationExtensions
{
    public static LoggerConfiguration UseReviewsDestructuring(this LoggerConfiguration configuration) =>
        configuration.Destructure.With<SensitiveDataDestructuringPolicy>()
                     .Destructure.ToMaximumCollectionCount(10)
                     .Destructure.ToMaximumDepth(4);
}