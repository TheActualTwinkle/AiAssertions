namespace AiAssertions.Core.Models;

/// <summary>
/// Represents a tool call requested by an AI model.
/// </summary>
public sealed record AiToolCall
{
    /// <summary>
    /// Gets the provider-assigned tool call identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the name of the tool to execute.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the JSON argument payload supplied by the model.
    /// </summary>
    public required string ArgumentsJson { get; init; }
}
