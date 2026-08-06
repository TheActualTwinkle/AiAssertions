using AiAssertions.Core.Abstractions;
using AiAssertions.Core.Models;

namespace AiAssertions.Tests;

internal sealed class CheckpointRecordingToolCallingClient(
    string checkpoint,
    params AiToolResponse[] responses) : IToolCallingClient
{
    private readonly Queue<AiToolResponse> _responses = new(responses);

    public AiModelRequestMetadata? RequestMetadata { get; init; } = new()
    {
        Provider = "Test",
        RequestedModel = "test-model",
        Temperature = 0.25
    };

    public Task<AiTextResponse> GetResponseAsync(
        AiTextRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new AiTextResponse { Content = checkpoint });

    public Task<AiToolResponse> GetToolResponseAsync(
        AiToolRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_responses.Dequeue());
}
