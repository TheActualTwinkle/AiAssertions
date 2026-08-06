namespace AiAssertions.Core.Assertions;

internal sealed record AiAssertionExecutionTrace
{
    internal required DateTimeOffset StartedAtUtc { get; init; }

    internal required DateTimeOffset CompletedAtUtc { get; init; }

    internal required TimeSpan Duration { get; init; }

    internal required IReadOnlyList<AiAssertionExecutionTraceEntry> Entries { get; init; }
}
