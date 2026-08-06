namespace AiAssertions;

/// <summary>
/// Represents the replacement of an older portion of conversation history by a checkpoint.
/// </summary>
public sealed record CodebaseAssertionConversationCompactionTraceEntry : CodebaseAssertionExecutionTraceEntry
{
    /// <summary>
    /// Gets the one-based tool iteration during which compaction occurred.
    /// </summary>
    public required int Iteration { get; init; }

    /// <summary>
    /// Gets the resulting checkpoint revision.
    /// </summary>
    public required int Revision { get; init; }

    /// <summary>
    /// Gets the index through which conversation messages were compacted.
    /// </summary>
    public required int CompactedThroughMessageIndex { get; init; }

    /// <summary>
    /// Gets the number of messages removed from the active conversation history.
    /// </summary>
    public required int RemovedMessageCount { get; init; }

    /// <summary>
    /// Gets the semantic summary stored in the conversation checkpoint.
    /// </summary>
    public required string SemanticSummary { get; init; }
}
