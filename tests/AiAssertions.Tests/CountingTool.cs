using AiAssertions.Core.Tools.Abstractions;

namespace AiAssertions.Tests;

internal sealed class CountingTool : IAiTool
{
    public string Name => "counting";

    public string Description => "Counts executions.";

    public string ParametersJsonSchema => """{"type":"object"}""";

    internal int Executions { get; private set; }

    public ValueTask<string> ExecuteAsync(
        string argumentsJson,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        Executions++;
        return ValueTask.FromResult("""{"value":1}""");
    }
}
