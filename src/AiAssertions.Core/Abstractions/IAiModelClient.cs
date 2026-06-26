using AiAssertions.Core.Models;

namespace AiAssertions.Core.Abstractions;

/// <summary>
/// Represents a provider-neutral client capable of producing text responses from chat messages.
/// </summary>
public interface IAiModelClient
{
    /// <summary>
    /// Sends a text request to the model and returns the generated response.
    /// </summary>
    /// <param name="request">The text request to send.</param>
    /// <param name="cancellationToken">A token used to cancel the request.</param>
    /// <returns>The model text response.</returns>
    Task<AiTextResponse> GetResponseAsync(AiTextRequest request, CancellationToken cancellationToken = default);
}
