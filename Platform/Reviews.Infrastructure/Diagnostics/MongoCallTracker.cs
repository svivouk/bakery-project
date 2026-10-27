using MongoDB.Driver.Core.Configuration;
using MongoDB.Driver.Core.Events;
using Reviews.Application.Common.Interfaces;

namespace Reviews.Infrastructure.Diagnostics;
public sealed class MongoCallTracker : IDatabaseCallTracker
{
    private static readonly AsyncLocal<Scope?> Current = new();

    public IDatabaseCallScope BeginScope()
    {
        var scope = new Scope(Current.Value);
        Current.Value = scope;
        return scope;
    }

    public void Record(string commandName, TimeSpan duration) => Current.Value?.Add(commandName, duration);
    public void Configure(ClusterBuilder builder) =>
        builder
            .Subscribe<CommandSucceededEvent>(e => Record(e.CommandName, e.Duration))
            .Subscribe<CommandFailedEvent>(e => Record(e.CommandName, e.Duration));

    private sealed class Scope(Scope? parent) : IDatabaseCallScope
    {
        private readonly object _gate = new();
        private int _count;
        private TimeSpan _total;
        private TimeSpan _slowest;
        private string? _slowestName;

        public int CommandCount { get { lock (_gate) return _count; } }
        public TimeSpan TotalDuration { get { lock (_gate) return _total; } }
        public TimeSpan SlowestDuration { get { lock (_gate) return _slowest; } }
        public string? SlowestCommand { get { lock (_gate) return _slowestName; } }

        public void Add(string name, TimeSpan duration)
        {
            lock (_gate)
            {
                _count++;
                _total += duration;
                if (duration > _slowest) { _slowest = duration; _slowestName = name; }
            }
        }

        public void Dispose() => Current.Value = parent;
    }
}