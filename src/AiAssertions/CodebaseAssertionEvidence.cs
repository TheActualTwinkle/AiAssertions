namespace AiAssertions;

/// <summary>
/// Represents concrete source evidence for a codebase assertion verdict.
/// </summary>
public sealed record CodebaseAssertionEvidence
{
    /// <summary>
    /// Gets the source file path relative to the inspected project root.
    /// </summary>
    public required string File { get; init; }

    /// <summary>
    /// Gets the first relevant one-based line number.
    /// </summary>
    public required int StartLine { get; init; }

    /// <summary>
    /// Gets the last relevant one-based line number.
    /// </summary>
    public required int EndLine { get; init; }

    /// <summary>
    /// Gets the concise evidence description.
    /// </summary>
    public required string Description { get; init; }
}