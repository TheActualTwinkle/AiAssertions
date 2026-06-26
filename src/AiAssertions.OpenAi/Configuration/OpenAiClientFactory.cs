using AiAssertions.Core.Abstractions;
using AiAssertions.OpenAi.Clients;

namespace AiAssertions.OpenAi.Configuration;

/// <summary>
/// Creates configured OpenAI clients.
/// </summary>
public static class OpenAiClientFactory
{
    /// <summary>
    /// Creates a new OpenAI client from the supplied options.
    /// </summary>
    /// <param name="options">The client options.</param>
    /// <returns>A configured tool-calling model client.</returns>
    public static IToolCallingClient Create(OpenAiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        
        return new OpenAiClient(new HttpClient { BaseAddress = options.Endpoint }, options);
    }
}
