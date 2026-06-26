namespace AiAssertions.OpenRouter.Configuration;

/// <summary>
/// Identifies the routed model used by the OpenRouter provider.
/// </summary>
public enum OpenRouterModel : byte
{
    /// <summary>
    /// The OpenAI gpt-4o model routed through OpenRouter.
    /// </summary>
    OpenAiGpt4O,

    /// <summary>
    /// The OpenAI gpt-4o-mini model routed through OpenRouter.
    /// </summary>
    OpenAiGpt4OMini,

    /// <summary>
    /// The OpenAI gpt-5.5 model routed through OpenRouter.
    /// </summary>
    OpenAiGpt55,

    /// <summary>
    /// The OpenAI gpt-5.4 model routed through OpenRouter.
    /// </summary>
    OpenAiGpt54,

    /// <summary>
    /// The DeepSeek chat model routed through OpenRouter.
    /// </summary>
    DeepSeekChat,

    /// <summary>
    /// The DeepSeek reasoner model routed through OpenRouter.
    /// </summary>
    DeepSeekReasoner,

    /// <summary>
    /// The DeepSeek v4 pro model routed through OpenRouter.
    /// </summary>
    DeepSeekV4Pro
}
