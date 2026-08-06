using AiAssertions.Core.Models;

namespace AiAssertions;

/// <summary>
/// Represents a request and response exchanged with the tool-calling model.
/// </summary>
public sealed record CodebaseAssertionModelExchangeTraceEntry : CodebaseAssertionExecutionTraceEntry
{
    /// <summary>
    /// Gets the request sent to the model.
    /// </summary>
    public required AiToolRequest Request { get; init; }

    /// <summary>
    /// Gets the model configuration recorded before the request was sent, when supplied by the client.
    /// </summary>
    public AiModelRequestMetadata? RequestMetadata { get; init; }

    /// <summary>
    /// Gets the response returned by the model, or <see langword="null"/> when the exchange failed.
    /// </summary>
    public AiToolResponse? Response { get; init; }

    /// <summary>
    /// Gets the error reported by a failed exchange, or <see langword="null"/> when it succeeded.
    /// </summary>
    public string? Error { get; init; }
}
