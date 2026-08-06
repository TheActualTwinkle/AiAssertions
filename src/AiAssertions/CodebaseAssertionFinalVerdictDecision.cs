namespace AiAssertions;

/// <summary>
/// Describes why the library produced its final assertion verdict.
/// </summary>
public enum CodebaseAssertionFinalVerdictDecision
{
    /// <summary>
    /// The model verdict was conclusive and met the configured confidence threshold.
    /// </summary>
    Accepted,

    /// <summary>
    /// The model returned a verdict but marked it as inconclusive.
    /// </summary>
    ModelInconclusive,

    /// <summary>
    /// The model verdict was conclusive but did not meet the configured confidence threshold.
    /// </summary>
    BelowConfidenceThreshold,

    /// <summary>
    /// The run ended before a model verdict was received.
    /// </summary>
    NoModelVerdict
}
