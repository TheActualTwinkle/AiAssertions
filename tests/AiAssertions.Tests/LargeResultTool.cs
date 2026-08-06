using AiAssertions.Core.Tools.Abstractions;

namespace AiAssertions.Tests;

internal sealed class LargeResultTool : IAiTool
{
    public string Name => "large_result";

    public string Description => "Returns a large result.";

    public string ParametersJsonSchema => """{"type":"object"}""";

    public ValueTask<string> ExecuteAsync(
        string argumentsJson,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(string.Concat("{\"data\":\"", new string('x', 40_000), "\"}"));
}
