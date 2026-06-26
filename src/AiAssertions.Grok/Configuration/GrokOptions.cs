namespace AiAssertions.Grok.Configuration;

/// <summary>
/// Configures the Grok client.
/// </summary>
public sealed class GrokOptions
{
    /// <summary>
    /// Gets the base endpoint for the xAI API.
    /// </summary>
    public Uri Endpoint { get; init; } = new("https://api.x.ai/v1/");

    /// <summary>
    /// Gets the relative chat completions path.
    /// </summary>
    public string ChatCompletionsPath { get; init; } = "chat/completions";

    /// <summary>
    /// Gets the model used for text and tool-calling requests.
    /// </summary>
    public GrokModel Model { get; init; } = GrokModel.Latest;

    /// <summary>
    /// Gets the API key used for bearer authentication.
    /// </summary>
    public required string ApiKey { get; init; }

    /// <summary>
    /// Gets the sampling temperature sent to the model.
    /// </summary>
    public double Temperature { get; init; }
}
