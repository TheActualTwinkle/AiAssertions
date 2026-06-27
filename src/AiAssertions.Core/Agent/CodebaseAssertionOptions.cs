namespace AiAssertions.Core.Agent;

internal sealed class CodebaseAssertionOptions
{
    public string? WorkingDirectory { get; init; }

    public int MaxToolIterations { get; init; } = 300;

    public int RecentToolCallTurns { get; init; } = 2;

    public int MaxCompactedToolResultChars { get; init; } = 1200;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(2);

    public double MinimumTrueConfidence { get; init; }

    public double MinimumFalseConfidence { get; init; }

    public IReadOnlyList<string> IncludedPaths { get; init; } = [];

    public IReadOnlyList<string> IncludedTypes { get; init; } = [];
}
