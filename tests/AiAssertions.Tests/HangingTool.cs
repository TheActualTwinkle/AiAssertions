using AiAssertions.Core.Tools.Abstractions;

namespace AiAssertions.Tests;

internal sealed class HangingTool : IAiTool
{
    public string Name => "counting";

    public string Description => "Never completes.";

    public string ParametersJsonSchema => """{"type":"object"}""";

    public ValueTask<string> ExecuteAsync(
        string argumentsJson,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default) =>
        new(new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously).Task);
}
