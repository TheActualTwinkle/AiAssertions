using AiAssertions.Anthropic.Clients;
using AiAssertions.Core.Abstractions;

namespace AiAssertions.Anthropic.Configuration;

/// <summary>
/// Creates configured Anthropic clients.
/// </summary>
public static class AnthropicClientFactory
{
    /// <summary>
    /// Creates a new Anthropic client from the supplied options.
    /// </summary>
    /// <param name="options">The client options.</param>
    /// <returns>A configured tool-calling model client.</returns>
    public static IToolCallingClient Create(AnthropicOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new AnthropicClient(new HttpClient { BaseAddress = options.Endpoint }, options);
    }
}
