namespace AiAssertions.OpenAi.Configuration;

/// <summary>
/// Identifies the OpenAI model used by the OpenAI provider.
/// </summary>
public enum OpenAiModel : byte
{
    /// <summary>
    /// The gpt-4.1-mini model.
    /// </summary>
    Gpt41Mini,

    /// <summary>
    /// The gpt-4o model.
    /// </summary>
    Gpt4O,

    /// <summary>
    /// The gpt-4o-mini model.
    /// </summary>
    Gpt4OMini,

    /// <summary>
    /// The gpt-5.5 model.
    /// </summary>
    Gpt55,

    /// <summary>
    /// The gpt-5.4 model.
    /// </summary>
    Gpt54
}
