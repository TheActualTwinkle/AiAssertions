namespace AiAssertions;

/// <summary>
/// Represents a verdict returned by the model BEFORE library confidence thresholds are applied.
/// </summary>
public sealed record CodebaseAssertionModelVerdictReceivedTraceEntry : CodebaseAssertionExecutionTraceEntry
{
    /// <summary>
    /// Gets a value indicating whether the model considered the requirement satisfied.
    /// </summary>
    public required bool Passed { get; init; }

    /// <summary>
    /// Gets the confidence reported by the model.
    /// </summary>
    public required double Confidence { get; init; }

    /// <summary>
    /// Gets a value indicating whether the model considered its verdict conclusive.
    /// </summary>
    public required bool IsConclusive { get; init; }

    /// <summary>
    /// Gets the reason reported by the model.
    /// </summary>
    public required string Reason { get; init; }
}
