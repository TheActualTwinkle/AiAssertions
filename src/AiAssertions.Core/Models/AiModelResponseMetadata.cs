namespace AiAssertions.Core.Models;

/// <summary>
/// Describes the model configuration and provider metadata associated with one model response.
/// </summary>
public sealed record AiModelResponseMetadata
{
    /// <summary>
    /// Gets the name of the provider that handled the request.
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Gets the model identifier sent in the request.
    /// </summary>
    public required string RequestedModel { get; init; }

    /// <summary>
    /// Gets the model identifier reported by the provider, when available.
    /// </summary>
    public string? ResponseModel { get; init; }

    /// <summary>
    /// Gets the sampling temperature sent in the request.
    /// </summary>
    public required double Temperature { get; init; }

    /// <summary>
    /// Gets the provider-specific reason why generation stopped, when available.
    /// </summary>
    public string? FinishReason { get; init; }

    /// <summary>
    /// Gets the token usage reported by the provider, when available.
    /// </summary>
    public AiTokenUsage? Usage { get; init; }
}
