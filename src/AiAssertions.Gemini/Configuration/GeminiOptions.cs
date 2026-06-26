namespace AiAssertions.Gemini.Configuration;

/// <summary>
/// Configures the Gemini client.
/// </summary>
public sealed class GeminiOptions
{
    /// <summary>
    /// Gets the base endpoint for the Gemini API.
    /// </summary>
    public Uri Endpoint { get; init; } = new("https://generativelanguage.googleapis.com/v1beta/");

    /// <summary>
    /// Gets the model used for text and tool-calling requests.
    /// </summary>
    public GeminiModel Model { get; init; } = GeminiModel.Gemini35Flash;

    /// <summary>
    /// Gets the API key used for Gemini authentication.
    /// </summary>
    public required string ApiKey { get; init; }

    /// <summary>
    /// Gets the sampling temperature sent to the model.
    /// </summary>
    public double Temperature { get; init; }
}
