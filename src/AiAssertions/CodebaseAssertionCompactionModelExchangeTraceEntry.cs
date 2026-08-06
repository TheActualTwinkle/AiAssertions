using AiAssertions.Core.Models;

namespace AiAssertions;

/// <summary>
/// Represents a text-only model exchange used to create a conversation checkpoint.
/// </summary>
public sealed record CodebaseAssertionCompactionModelExchangeTraceEntry : CodebaseAssertionExecutionTraceEntry
{
    /// <summary>
    /// Gets the request sent to the model.
    /// </summary>
    public required AiTextRequest Request { get; init; }

    /// <summary>
    /// Gets the response returned by the model, or <see langword="null"/> when the exchange failed.
    /// </summary>
    public AiTextResponse? Response { get; init; }

    /// <summary>
    /// Gets the error reported by a failed exchange, or <see langword="null"/> when it succeeded.
    /// </summary>
    public string? Error { get; init; }
}
