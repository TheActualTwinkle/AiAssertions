using AiAssertions.Core.Abstractions;
using AiAssertions.Core.Models;

namespace AiAssertions.Tests;

internal sealed class HangingClient : IToolCallingClient
{
    public AiModelRequestMetadata? RequestMetadata { get; init; } = new()
    {
        Provider = "Test",
        RequestedModel = "hanging-model",
        Temperature = 0.5
    };

    public Task<AiToolResponse> GetToolResponseAsync(
        AiToolRequest request,
        CancellationToken cancellationToken = default) =>
        new TaskCompletionSource<AiToolResponse>(TaskCreationOptions.RunContinuationsAsynchronously).Task;

    public Task<AiTextResponse> GetResponseAsync(
        AiTextRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
