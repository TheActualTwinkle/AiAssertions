namespace AiAssertions.Core.Models;

/// <summary>
/// Describes a tool that can be exposed to an AI model.
/// </summary>
public sealed record AiToolDefinition
{
    /// <summary>
    /// Gets the tool name visible to the model.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the human-readable tool description visible to the model.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the JSON schema describing the tool arguments.
    /// </summary>
    public required string ParametersJsonSchema { get; init; }
}
