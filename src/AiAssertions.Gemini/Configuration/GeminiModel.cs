namespace AiAssertions.Gemini.Configuration;

/// <summary>
/// Identifies the Gemini model used by the Gemini provider.
/// </summary>
public enum GeminiModel : byte
{
    /// <summary>
    /// The gemini-3.5-flash model.
    /// </summary>
    Gemini35Flash,

    /// <summary>
    /// The gemini-3.5-pro model.
    /// </summary>
    Gemini35Pro,

    /// <summary>
    /// The gemini-2.5-flash model.
    /// </summary>
    Gemini25Flash,

    /// <summary>
    /// The gemini-2.5-pro model.
    /// </summary>
    Gemini25Pro
}
