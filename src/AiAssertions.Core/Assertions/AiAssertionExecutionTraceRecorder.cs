using System.Diagnostics;
using System.Text.Json;

namespace AiAssertions.Core.Assertions;

internal sealed class AiAssertionExecutionTraceRecorder
{
    private readonly List<AiAssertionExecutionTraceEntry> _entries = [];
    private readonly object _syncRoot = new();
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private int _sequence;

    internal AiAssertionExecutionTraceRecorder() =>
        StartedAtUtc = DateTimeOffset.UtcNow;

    internal DateTimeOffset StartedAtUtc { get; }

    internal AiAssertionExecutionTraceActivity Begin() =>
        new(
            Interlocked.Increment(ref _sequence),
            DateTimeOffset.UtcNow,
            Stopwatch.GetTimestamp());

    internal void Complete(
        AiAssertionExecutionTraceActivity activity,
        AiAssertionExecutionTraceEntryKind kind,
        string name,
        object payload)
    {
        var entry = new AiAssertionExecutionTraceEntry
        {
            Sequence = activity.Sequence,
            StartedAtUtc = activity.StartedAtUtc,
            Duration = Stopwatch.GetElapsedTime(activity.StartedTimestamp),
            Kind = kind,
            Name = name,
            PayloadJson = JsonSerializer.Serialize(payload, AssertionJson.Options)
        };

        lock (_syncRoot)
            _entries.Add(entry);
    }

    internal void Record(AiAssertionExecutionTraceEntryKind kind, string name, object payload)
    {
        var activity = Begin();
        Complete(activity, kind, name, payload);
    }

    internal AiAssertionExecutionTrace Snapshot()
    {
        _stopwatch.Stop();
        var completedAtUtc = DateTimeOffset.UtcNow;
        AiAssertionExecutionTraceEntry[] entries;

        lock (_syncRoot)
            entries = _entries.OrderBy(entry => entry.Sequence).ToArray();

        return new AiAssertionExecutionTrace
        {
            StartedAtUtc = StartedAtUtc,
            CompletedAtUtc = completedAtUtc,
            Duration = _stopwatch.Elapsed,
            Entries = entries
        };
    }
}
