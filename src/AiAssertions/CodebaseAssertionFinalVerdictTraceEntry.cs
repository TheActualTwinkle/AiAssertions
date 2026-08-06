namespace AiAssertions;

/// <summary>
/// Represents the final verdict produced by the library AFTER configured confidence thresholds are applied.
/// </summary>
public sealed record CodebaseAssertionFinalVerdictTraceEntry : CodebaseAssertionExecutionTraceEntry
{
    /// <summary>
    /// Gets the final library verdict.
    /// </summary>
    public required CodebaseAssertionVerdict Verdict { get; init; }

    /// <summary>
    /// Gets the confidence used to produce the final verdict. The value is zero when no model verdict was received.
    /// </summary>
    public required double Confidence { get; init; }

    /// <summary>
    /// Gets a value indicating whether the run received a parsed model verdict.
    /// </summary>
    public required bool ModelVerdictReceived { get; init; }

    /// <summary>
    /// Gets whether the model considered the requirement satisfied, or <see langword="null"/> when no model verdict was received.
    /// </summary>
    public bool? ModelPassed { get; init; }

    /// <summary>
    /// Gets whether the model considered its verdict conclusive, or <see langword="null"/> when no model verdict was received.
    /// </summary>
    public bool? ModelIsConclusive { get; init; }

    /// <summary>
    /// Gets the confidence threshold applied to a conclusive model verdict, or <see langword="null"/> when no threshold was applied.
    /// </summary>
    public double? AppliedConfidenceThreshold { get; init; }

    /// <summary>
    /// Gets the reason the library produced this final verdict.
    /// </summary>
    public required CodebaseAssertionFinalVerdictDecision Decision { get; init; }

    /// <summary>
    /// Gets the final library comment, including any confidence-tolerance explanation.
    /// </summary>
    public required string Comment { get; init; }
}
