namespace AiAssertions.Core.Models;

/// <summary>
/// Represents a text-only request sent to an AI model.
/// </summary>
public sealed record AiTextRequest
{
    /// <summary>
    /// Gets the chat messages included in the request.
    /// </summary>
    public required IReadOnlyList<AiChatMessage> Messages { get; init; }
}
