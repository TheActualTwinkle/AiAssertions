using AiAssertions.Core.Abstractions;
using AiAssertions.Core.Assertions;
using AiAssertions.Core.Models;

namespace AiAssertions.Core.Agent;

internal sealed class ExecutionTraceRecordingClient(
    IToolCallingClient inner,
    AiAssertionExecutionTraceRecorder recorder) : IToolCallingClient
{
    public async Task<AiTextResponse> GetResponseAsync(
        AiTextRequest request,
        CancellationToken cancellationToken = default)
    {
        var activity = recorder.Begin();
        var requestMetadata = inner.RequestMetadata;

        try
        {
            var response = await inner
                .GetResponseAsync(request, cancellationToken)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            recorder.Complete(
                activity,
                AiAssertionExecutionTraceEntryKind.ConversationCompactionModelExchange,
                "conversation_checkpoint",
                new { request, requestMetadata, response });
            return response;
        }
        catch (Exception exception)
        {
            recorder.Complete(
                activity,
                AiAssertionExecutionTraceEntryKind.ConversationCompactionModelExchange,
                "conversation_checkpoint",
                new { request, requestMetadata, error = exception.ToString() });
            throw;
        }
    }

    public async Task<AiToolResponse> GetToolResponseAsync(
        AiToolRequest request,
        CancellationToken cancellationToken = default)
    {
        var activity = recorder.Begin();
        var requestMetadata = inner.RequestMetadata;

        try
        {
            var response = await inner
                .GetToolResponseAsync(request, cancellationToken)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            recorder.Complete(
                activity,
                AiAssertionExecutionTraceEntryKind.ModelExchange,
                "tool_iteration",
                new { request, requestMetadata, response });
            return response;
        }
        catch (Exception exception)
        {
            recorder.Complete(
                activity,
                AiAssertionExecutionTraceEntryKind.ModelExchange,
                "tool_iteration",
                new { request, requestMetadata, error = exception.ToString() });
            throw;
        }
    }
}
