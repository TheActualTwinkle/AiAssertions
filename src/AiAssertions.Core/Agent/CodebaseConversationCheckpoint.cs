using AiAssertions.Core.Models;

namespace AiAssertions.Core.Agent;

internal sealed class CodebaseConversationCheckpoint
{
    internal int CompactedThroughMessageIndex { get; set; } = 2;

    internal string SemanticSummary { get; set; } = string.Empty;

    internal int Revision { get; set; }

    internal List<CompactedToolCoverage> Coverage { get; } = [];

    internal Dictionary<string, int> CoverageIndexes { get; } = new(StringComparer.Ordinal);

    internal void PruneCompactedPrefix(List<AiChatMessage> messages)
    {
        var removeCount = Math.Min(
            Math.Max(CompactedThroughMessageIndex - 2, 0),
            Math.Max(messages.Count - 2, 0));
        if (removeCount == 0)
            return;

        messages.RemoveRange(2, removeCount);
        CompactedThroughMessageIndex = 2;
    }
}
