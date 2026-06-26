namespace AiAssertions;

/// <summary>
/// Represents the result of an AI-powered codebase assertion.
/// </summary>
public sealed record CodebaseAssertionResult
{
    /// <summary>
    /// Gets the computed assertion verdict.
    /// </summary>
    public required CodebaseAssertionVerdict Verdict { get; init; }

    /// <summary>
    /// Gets the model confidence used to compute the verdict.
    /// </summary>
    public required double Confidence { get; init; }

    /// <summary>
    /// Gets the concise model-generated explanation for the verdict.
    /// </summary>
    public required string Comment { get; init; }

    /// <summary>
    /// Gets the concrete code evidence returned by the model.
    /// </summary>
    public required IReadOnlyList<CodebaseAssertionEvidence> Evidence { get; init; }

    /// <summary>
    /// Gets evidence that was expected or needed but not found by the model.
    /// </summary>
    public required IReadOnlyList<CodebaseAssertionMissingEvidence> MissingEvidence { get; init; }
}
