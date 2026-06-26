namespace AiAssertions.Anthropic.Configuration;

/// <summary>
/// Identifies the Claude model used by the Anthropic provider.
/// </summary>
public enum AnthropicModel : byte
{
    /// <summary>
    /// The claude-sonnet-4-5 model alias.
    /// </summary>
    ClaudeSonnet45,

    /// <summary>
    /// The claude-haiku-4-5 model alias.
    /// </summary>
    ClaudeHaiku45,

    /// <summary>
    /// The claude-opus-4-8 model alias.
    /// </summary>
    ClaudeOpus48
}
