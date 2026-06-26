namespace AiAssertions.Anthropic.Configuration;

/// <summary>
/// Configures the Anthropic client.
/// </summary>
public sealed class AnthropicOptions
{
    /// <summary>
    /// Gets the base endpoint for the Anthropic API.
    /// </summary>
    public Uri Endpoint { get; init; } = new("https://api.anthropic.com/v1/");

    /// <summary>
    /// Gets the relative messages path.
    /// </summary>
    public string MessagesPath { get; init; } = "messages";

    /// <summary>
    /// Gets the model used for text and tool-calling requests.
    /// </summary>
    public AnthropicModel Model { get; init; } = AnthropicModel.ClaudeSonnet45;

    /// <summary>
    /// Gets the API key used for Anthropic authentication.
    /// </summary>
    public required string ApiKey { get; init; }

    /// <summary>
    /// Gets the Anthropic API version header value.
    /// </summary>
    public string ApiVersion { get; init; } = "2023-06-01";

    /// <summary>
    /// Gets the maximum number of output tokens.
    /// </summary>
    public int MaxTokens { get; init; } = 4096;

    /// <summary>
    /// Gets the sampling temperature sent to the model.
    /// </summary>
    public double Temperature { get; init; }
}
