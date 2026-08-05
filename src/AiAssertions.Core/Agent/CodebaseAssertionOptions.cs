using AiAssertions.Core.Models;

namespace AiAssertions.Core.Agent;

internal sealed class CodebaseAssertionOptions
{
    public string? SystemPrompt { get; init; }

    public string? AdditionalSystemPrompt { get; init; }

    public string? WorkingDirectory { get; init; }

    public int MaxToolIterations { get; init; } = 300;

    public int? MaxRequestTokens { get; init; }

    public Func<IReadOnlyList<AiChatMessage>, int>? RequestTokenEstimator { get; init; }

    public bool ConversationCompactionEnabled { get; init; } = true;

    public int RecentToolCallTurns { get; init; } = 2;

    public int MaxCompactedToolResultChars { get; init; } = 3000;

    public int MaxCompactedStateChars { get; init; } = 16_000;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(2);

    public double MinimumTrueConfidence { get; init; }

    public double MinimumFalseConfidence { get; init; }

    public IReadOnlyList<string> IncludedPaths { get; init; } = [];

    public IReadOnlyList<string> IncludedTypes { get; init; } = [];
}
