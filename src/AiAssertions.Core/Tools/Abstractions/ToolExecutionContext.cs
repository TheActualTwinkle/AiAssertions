namespace AiAssertions.Core.Tools.Abstractions;

/// <summary>
/// Provides contextual information for local tool execution.
/// </summary>
internal sealed class ToolExecutionContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ToolExecutionContext"/> class.
    /// </summary>
    /// <param name="workingDirectory">The working directory used as the tool execution base.</param>
    internal ToolExecutionContext(string workingDirectory) =>
        WorkingDirectory = Path.GetFullPath(workingDirectory);

    /// <summary>
    /// Gets the normalized working directory used by tools.
    /// </summary>
    internal string WorkingDirectory { get; }
}
