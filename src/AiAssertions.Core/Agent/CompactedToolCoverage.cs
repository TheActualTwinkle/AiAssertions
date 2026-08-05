namespace AiAssertions.Core.Agent;

internal sealed class CompactedToolCoverage
{
    internal required string Tool { get; init; }

    internal required string ArgumentsJson { get; init; }

    internal required string Outcome { get; set; }

    internal int Repetitions { get; set; } = 1;
}
