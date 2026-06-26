using AiAssertions.Core.Abstractions;
using AiAssertions.Gemini.Clients;

namespace AiAssertions.Gemini.Configuration;

/// <summary>
/// Creates configured Gemini clients.
/// </summary>
public static class GeminiClientFactory
{
    /// <summary>
    /// Creates a new Gemini client from the supplied options.
    /// </summary>
    /// <param name="options">The client options.</param>
    /// <returns>A configured tool-calling model client.</returns>
    public static IToolCallingClient Create(GeminiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new GeminiClient(new HttpClient { BaseAddress = options.Endpoint }, options);
    }
}
