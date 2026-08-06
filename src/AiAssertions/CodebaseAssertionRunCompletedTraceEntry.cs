namespace AiAssertions;

/// <summary>
/// Represents the result reached by an assertion run.
/// </summary>
public sealed record CodebaseAssertionRunCompletedTraceEntry : CodebaseAssertionExecutionTraceEntry
{
    /// <summary>
    /// Gets a value indicating whether the requirement passed.
    /// </summary>
    public required bool Passed { get; init; }

    /// <summary>
    /// Gets the model confidence in the result.
    /// </summary>
    public required double Confidence { get; init; }

    /// <summary>
    /// Gets a value indicating whether the result is conclusive.
    /// </summary>
    public required bool IsConclusive { get; init; }

    /// <summary>
    /// Gets the concise reason for the result.
    /// </summary>
    public required string Reason { get; init; }
}
