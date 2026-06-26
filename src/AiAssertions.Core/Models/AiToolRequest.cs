namespace AiAssertions.Core.Models;

/// <summary>
/// Represents a tool-aware request sent to an AI model.
/// </summary>
public sealed record AiToolRequest
{
    /// <summary>
    /// Gets the chat messages included in the request.
    /// </summary>
    public required IReadOnlyList<AiChatMessage> Messages { get; init; }

    /// <summary>
    /// Gets the tools available to the model.
    /// </summary>
    public required IReadOnlyList<AiToolDefinition> Tools { get; init; }
}
