namespace AiAssertions;

internal sealed record AiAssertDefaults
{
    internal TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(2);

    internal int MaxToolIterations { get; init; } = 300;

    internal double MinimumTrueConfidence { get; init; }

    internal double MinimumFalseConfidence { get; init; }
}
