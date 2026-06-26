namespace AiAssertions.Grok.Configuration;

/// <summary>
/// Identifies the Grok model used by the Grok provider.
/// </summary>
public enum GrokModel : byte
{
    /// <summary>
    /// The latest generally available Grok model alias.
    /// </summary>
    Latest,

    /// <summary>
    /// The grok-4 model.
    /// </summary>
    Grok4,

    /// <summary>
    /// The grok-3 model.
    /// </summary>
    Grok3,

    /// <summary>
    /// The grok-3-mini model.
    /// </summary>
    Grok3Mini
}
