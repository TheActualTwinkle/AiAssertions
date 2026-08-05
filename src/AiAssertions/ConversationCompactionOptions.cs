namespace AiAssertions;

/// <summary>
/// Configures adaptive semantic checkpointing for codebase assertion conversations.
/// </summary>
public sealed class ConversationCompactionOptions
{
    /// <summary>
    /// Gets the number of newest tool-call turns protected from checkpointing.
    /// </summary>
    public int RecentToolCallTurns { get; init; } = 2;

    /// <summary>
    /// Gets the maximum number of characters retained in the semantic checkpoint and deterministic coverage ledger.
    /// </summary>
    public int MaxCheckpointChars { get; init; } = 16_000;

    internal void Validate()
    {
        if (RecentToolCallTurns <= 0)
            throw new ArgumentOutOfRangeException(nameof(RecentToolCallTurns), "Recent tool-call turns must be positive.");
        if (MaxCheckpointChars <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxCheckpointChars), "Maximum checkpoint characters must be positive.");
    }
}
