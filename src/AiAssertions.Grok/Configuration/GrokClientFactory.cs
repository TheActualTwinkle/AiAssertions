using AiAssertions.Core.Abstractions;
using AiAssertions.Grok.Clients;

namespace AiAssertions.Grok.Configuration;

/// <summary>
/// Creates configured Grok clients.
/// </summary>
public static class GrokClientFactory
{
    /// <summary>
    /// Creates a new Grok client from the supplied options.
    /// </summary>
    /// <param name="options">The client options.</param>
    /// <returns>A configured tool-calling model client.</returns>
    public static IToolCallingClient Create(GrokOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new GrokClient(new HttpClient { BaseAddress = options.Endpoint }, options);
    }
}
