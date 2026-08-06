namespace AiAssertions.Core.Models;

/// <summary>
/// Describes the model configuration associated with one model request.
/// </summary>
public sealed record AiModelRequestMetadata
{
    /// <summary>
    /// Gets the name of the configured model provider.
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Gets the model identifier sent in the request.
    /// </summary>
    public required string RequestedModel { get; init; }

    /// <summary>
    /// Gets the sampling temperature sent in the request.
    /// </summary>
    public required double Temperature { get; init; }
}
