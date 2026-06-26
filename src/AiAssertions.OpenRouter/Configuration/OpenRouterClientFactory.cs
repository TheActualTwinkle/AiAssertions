using AiAssertions.Core.Abstractions;
using AiAssertions.OpenRouter.Clients;

namespace AiAssertions.OpenRouter.Configuration;

/// <summary>
/// Creates configured OpenRouter clients.
/// </summary>
public static class OpenRouterClientFactory
{
    /// <summary>
    /// Creates a new OpenRouter client from an API key and model selection.
    /// </summary>
    /// <param name="apiKey">The OpenRouter API key.</param>
    /// <param name="model">The routed model to use.</param>
    /// <returns>A configured tool-calling model client.</returns>
    public static IToolCallingClient Create(string apiKey, OpenRouterModel model = OpenRouterModel.OpenAiGpt4O) =>
        Create(new OpenRouterOptions
        {
            ApiKey = apiKey,
            Model = model
        });

    /// <summary>
    /// Creates a new OpenRouter client from the supplied options.
    /// </summary>
    /// <param name="options">The client options.</param>
    /// <returns>A configured tool-calling model client.</returns>
    public static IToolCallingClient Create(OpenRouterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ApiKey);

        return new OpenRouterClient(new HttpClient { BaseAddress = new Uri("https://openrouter.ai/api/v1/") }, options);
    }
}
