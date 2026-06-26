namespace AiAssertions.Core.Models;

/// <summary>
/// Represents a chat message exchanged with an AI model.
/// </summary>
public sealed record AiChatMessage
{
    /// <summary>
    /// Gets the message role, such as system, user, assistant, or tool.
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// Gets the textual message content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets the optional message name used by some provider protocols.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the tool call identifier associated with a tool response message.
    /// </summary>
    public string? ToolCallId { get; init; }

    /// <summary>
    /// Gets the tool calls requested by an assistant message.
    /// </summary>
    public IReadOnlyList<AiToolCall>? ToolCalls { get; init; }
}
