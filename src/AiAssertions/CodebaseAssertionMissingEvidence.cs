namespace AiAssertions;

/// <summary>
/// Represents relevant evidence that was expected or needed but not found.
/// </summary>
public sealed record CodebaseAssertionMissingEvidence
{
    /// <summary>
    /// Gets the concise description of the missing evidence.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the optional file, directory, symbol, or area where the evidence was expected.
    /// </summary>
    public string? ExpectedLocation { get; init; }
}
