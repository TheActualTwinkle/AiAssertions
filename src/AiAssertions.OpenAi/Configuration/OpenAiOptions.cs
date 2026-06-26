namespace AiAssertions.OpenAi.Configuration;

/// <summary>
/// Configures the OpenAI client.
/// </summary>
public sealed class OpenAiOptions
{
    /// <summary>
    /// Gets the base endpoint for the OpenAI API.
    /// </summary>
    public Uri Endpoint { get; init; } = new("https://api.openai.com/v1/");

    /// <summary>
    /// Gets the relative chat completions path.
    /// </summary>
    public string ChatCompletionsPath { get; init; } = "chat/completions";

    /// <summary>
    /// Gets the model used for text and tool-calling requests.
    /// </summary>
    public OpenAiModel Model { get; init; } = OpenAiModel.Gpt55;

    /// <summary>
    /// Gets the API key used for bearer authentication.
    /// </summary>
    public required string ApiKey { get; init; }

    /// <summary>
    /// Gets the sampling temperature sent to the model.
    /// </summary>
    public double Temperature { get; init; }

    /// <summary>
    /// Gets the optional HTTP referer header value for compatible gateways.
    /// </summary>
    public string? HttpReferer { get; init; }

    /// <summary>
    /// Gets the optional title header value for compatible gateways.
    /// </summary>
    public string? Title { get; init; }
}
