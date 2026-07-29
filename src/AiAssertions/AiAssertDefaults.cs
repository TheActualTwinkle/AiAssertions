using AiAssertions.Core.Models;

namespace AiAssertions;

internal sealed record AiAssertDefaults
{
    internal TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(2);

    internal int MaxToolIterations { get; init; } = 300;

    internal int? MaxRequestTokens { get; init; }

    internal Func<IReadOnlyList<AiChatMessage>, int>? RequestTokenEstimator { get; init; }

    internal bool ConversationCompactionEnabled { get; init; } = true;

    internal int RecentToolCallTurns { get; init; } = 2;

    internal int MaxCompactedToolResultChars { get; init; } = 3000;

    internal Func<IReadOnlyList<AiChatMessage>, IReadOnlyList<AiChatMessage>>? ConversationCompactor { get; init; }

    internal double MinimumTrueConfidence { get; init; }

    internal double MinimumFalseConfidence { get; init; }
}
