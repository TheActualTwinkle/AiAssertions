namespace AiAssertions.DeepSeek.Configuration;

/// <summary>
/// Identifies the DeepSeek model used by the DeepSeek provider.
/// </summary>
public enum DeepSeekModel : byte
{
    /// <summary>
    /// The deepseek-chat model.
    /// </summary>
    Chat,

    /// <summary>
    /// The deepseek-reasoner model.
    /// </summary>
    Reasoner,

    /// <summary>
    /// The deepseek-v4-pro model.
    /// </summary>
    V4Pro
}
