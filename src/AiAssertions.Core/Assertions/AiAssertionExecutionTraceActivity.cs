namespace AiAssertions.Core.Assertions;

internal readonly record struct AiAssertionExecutionTraceActivity(
    int Sequence,
    DateTimeOffset StartedAtUtc,
    long StartedTimestamp);
