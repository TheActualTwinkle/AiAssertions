namespace AiAssertions.OpenRouter.Configuration;

/// <summary>
/// Configures the OpenRouter client.
/// </summary>
public sealed class OpenRouterOptions
{
    /// <summary>
    /// Gets the API key used for bearer authentication.
    /// </summary>
    public required string ApiKey { get; init; }

    /// <summary>
    /// Gets the routed model used for text and tool-calling requests.
    /// </summary>
    public OpenRouterModel Model { get; init; } = OpenRouterModel.OpenAiGpt4O;

    /// <summary>
    /// Gets the optional HTTP-Referer header value sent to OpenRouter.
    /// </summary>
    public string? HttpReferer { get; init; } = "https://github.com/aiassert/aiassertions";

    /// <summary>
    /// Gets the optional X-Title header value sent to OpenRouter.
    /// </summary>
    public string? Title { get; init; } = "AiAssertions Sample";

    /// <summary>
    /// Gets the sampling temperature sent to the routed model.
    /// </summary>
    public double Temperature { get; init; }
}
