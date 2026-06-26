using AiAssertions.Core.Models;

namespace AiAssertions.Core.Abstractions;

/// <summary>
/// Represents a model client that supports native tool or function calling.
/// </summary>
public interface IToolCallingClient : IAiModelClient
{
    /// <summary>
    /// Sends a tool-aware request to the model and returns either tool calls or final content.
    /// </summary>
    /// <param name="request">The tool-aware request to send.</param>
    /// <param name="cancellationToken">A token used to cancel the request.</param>
    /// <returns>The model response containing tool calls or final content.</returns>
    Task<AiToolResponse> GetToolResponseAsync(AiToolRequest request, CancellationToken cancellationToken = default);
}
