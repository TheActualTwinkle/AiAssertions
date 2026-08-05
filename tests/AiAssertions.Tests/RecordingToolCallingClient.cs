using AiAssertions.Core.Abstractions;
using AiAssertions.Core.Models;

namespace AiAssertions.Tests;

internal sealed class RecordingToolCallingClient(params AiToolResponse[] responses) : IToolCallingClient
{
    private readonly Queue<AiToolResponse> _responses = new(responses);

    internal List<AiToolRequest> Requests { get; } = [];

    public Task<AiToolResponse> GetToolResponseAsync(
        AiToolRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);

        return Task.FromResult(_responses.Dequeue());
    }

    public Task<AiTextResponse> GetResponseAsync(
        AiTextRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
