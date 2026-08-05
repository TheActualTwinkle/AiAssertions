using AiAssertions.Core.Abstractions;
using AiAssertions.Core.Models;

namespace AiAssertions.Tests;

internal sealed class CheckpointClient(string checkpoint) : IToolCallingClient
{
    internal List<AiTextRequest> Requests { get; } = [];

    public Task<AiTextResponse> GetResponseAsync(
        AiTextRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.FromResult(new AiTextResponse { Content = checkpoint });
    }

    public Task<AiToolResponse> GetToolResponseAsync(
        AiToolRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
