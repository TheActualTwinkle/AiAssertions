using System.Text.Json;

namespace AiAssertions;

/// <summary>
/// Represents a local tool execution or a result served from the per-run cache.
/// </summary>
public sealed record CodebaseAssertionToolExecutionTraceEntry : CodebaseAssertionExecutionTraceEntry
{
    /// <summary>
    /// Gets the identifier assigned to the tool call by the model.
    /// </summary>
    public required string ToolCallId { get; init; }

    /// <summary>
    /// Gets the name of the executed tool.
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Gets the tool-specific arguments supplied by the model.
    /// </summary>
    public required JsonElement Arguments { get; init; }

    /// <summary>
    /// Gets the tool-specific result, or <see langword="null"/> when execution failed.
    /// </summary>
    public JsonElement? Result { get; init; }

    /// <summary>
    /// Gets a value indicating whether the result was served from the per-run cache.
    /// </summary>
    public required bool CacheHit { get; init; }

    /// <summary>
    /// Gets the error reported by a failed execution, or <see langword="null"/> when it succeeded.
    /// </summary>
    public string? Error { get; init; }
}
