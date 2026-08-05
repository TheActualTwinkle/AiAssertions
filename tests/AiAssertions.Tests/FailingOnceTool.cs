using AiAssertions.Core.Tools.Abstractions;

namespace AiAssertions.Tests;

internal sealed class FailingOnceTool : IAiTool
{
    public string Name => "counting";

    public string Description => "Fails once.";

    public string ParametersJsonSchema => """{"type":"object"}""";

    internal int Executions { get; private set; }

    public ValueTask<string> ExecuteAsync(
        string argumentsJson,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        Executions++;
        return Executions == 1
            ? throw new InvalidOperationException("Transient failure.")
            : ValueTask.FromResult("""{"value":1}""");
    }
}
