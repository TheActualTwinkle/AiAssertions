namespace AiAssertions.Core.Tools.Abstractions;

/// <summary>
/// Represents a reusable tool that an AI agent can call through a model provider.
/// </summary>
internal interface IAiTool
{
    /// <summary>
    /// Gets the tool name exposed to the model.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the tool description exposed to the model.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the JSON schema for the tool argument object.
    /// </summary>
    string ParametersJsonSchema { get; }

    /// <summary>
    /// Executes the tool with the supplied JSON arguments.
    /// </summary>
    /// <param name="argumentsJson">The JSON argument payload supplied by the model.</param>
    /// <param name="context">The execution context for local tool execution.</param>
    /// <param name="cancellationToken">A token used to cancel tool execution.</param>
    /// <returns>A JSON string containing the tool result.</returns>
    ValueTask<string> ExecuteAsync(string argumentsJson, ToolExecutionContext context, CancellationToken cancellationToken = default);
}
