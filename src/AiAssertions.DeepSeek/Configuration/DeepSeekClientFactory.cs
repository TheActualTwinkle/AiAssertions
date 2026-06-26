using AiAssertions.Core.Abstractions;
using AiAssertions.DeepSeek.Clients;

namespace AiAssertions.DeepSeek.Configuration;

/// <summary>
/// Creates configured DeepSeek clients.
/// </summary>
public static class DeepSeekClientFactory
{
    /// <summary>
    /// Creates a new DeepSeek client from the supplied options.
    /// </summary>
    /// <param name="options">The client options.</param>
    /// <returns>A configured tool-calling model client.</returns>
    public static IToolCallingClient Create(DeepSeekOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        
        return new DeepSeekClient(new HttpClient { BaseAddress = options.Endpoint }, options);
    }
}
