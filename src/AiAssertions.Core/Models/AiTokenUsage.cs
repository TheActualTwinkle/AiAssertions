namespace AiAssertions.Core.Models;

/// <summary>
/// Represents provider-reported token usage for one model response.
/// </summary>
public sealed record AiTokenUsage
{
    /// <summary>
    /// Gets the number of input or prompt tokens, when reported.
    /// </summary>
    public long? PromptTokens { get; init; }

    /// <summary>
    /// Gets the number of output or completion tokens, when reported.
    /// </summary>
    public long? CompletionTokens { get; init; }

    /// <summary>
    /// Gets the total number of tokens, when reported.
    /// </summary>
    public long? TotalTokens { get; init; }

    /// <summary>
    /// Gets the number of cached input tokens, when reported.
    /// </summary>
    public long? CachedTokens { get; init; }

    /// <summary>
    /// Gets the number of reasoning or thinking tokens, when reported.
    /// </summary>
    public long? ReasoningTokens { get; init; }
}
