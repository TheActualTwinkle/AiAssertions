namespace AiAssertions.Core.Assertions;

internal sealed record AiAssertionExecutionTraceEntry
{
    internal required int Sequence { get; init; }

    internal required DateTimeOffset StartedAtUtc { get; init; }

    internal required TimeSpan Duration { get; init; }

    internal required AiAssertionExecutionTraceEntryKind Kind { get; init; }

    internal required string Name { get; init; }

    internal required string PayloadJson { get; init; }
}
