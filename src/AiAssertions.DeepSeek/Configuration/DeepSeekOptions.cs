namespace AiAssertions.DeepSeek.Configuration;

/// <summary>
/// Configures the DeepSeek client.
/// </summary>
public sealed class DeepSeekOptions
{
    /// <summary>
    /// Gets the base endpoint for the DeepSeek API.
    /// </summary>
    public Uri Endpoint { get; init; } = new("https://api.deepseek.com/v1/");

    /// <summary>
    /// Gets the relative chat completions path.
    /// </summary>
    public string ChatCompletionsPath { get; init; } = "chat/completions";

    /// <summary>
    /// Gets the model used for text and tool-calling requests.
    /// </summary>
    public DeepSeekModel Model { get; init; } = DeepSeekModel.Chat;

    /// <summary>
    /// Gets the API key used for bearer authentication.
    /// </summary>
    public required string ApiKey { get; init; }

    /// <summary>
    /// Gets the sampling temperature sent to the model.
    /// </summary>
    public double Temperature { get; init; }
}
