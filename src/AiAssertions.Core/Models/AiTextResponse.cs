namespace AiAssertions.Core.Models;

/// <summary>
/// Represents a text response returned by an AI model.
/// </summary>
public sealed record AiTextResponse
{
    /// <summary>
    /// Gets the response text content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets model configuration and provider response metadata, when supplied by the client.
    /// </summary>
    public AiModelResponseMetadata? Metadata { get; init; }
}
