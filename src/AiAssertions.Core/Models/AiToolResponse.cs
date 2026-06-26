namespace AiAssertions.Core.Models;

/// <summary>
/// Represents a response from a tool-calling AI model.
/// </summary>
public sealed record AiToolResponse
{
    /// <summary>
    /// Gets the final assistant content, when no further tool calls are requested.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// Gets the tool calls requested by the model.
    /// </summary>
    public required IReadOnlyList<AiToolCall> ToolCalls { get; init; }
}
