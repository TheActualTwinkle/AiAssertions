namespace AiAssertions;

/// <summary>
/// Describes the computed result of a codebase assertion.
/// </summary>
public enum CodebaseAssertionVerdict : byte
{
    /// <summary>
    /// The requirement is satisfied.
    /// </summary>
    Passed,

    /// <summary>
    /// The requirement is not satisfied.
    /// </summary>
    Failed,

    /// <summary>
    /// The model could not make a conclusive determination.
    /// </summary>
    NotDetermined
}
