using AiAssertions.Core.Tools.Abstractions;

namespace AiAssertions.Core.Tools.Codebase;

internal sealed class DiscoverProjectRootTool : JsonTool<DiscoverProjectRootToolArguments>
{
    public override string Name => "discover_project_root";

    public override string Description => "Discovers the nearest project root by walking upward from the working directory.";

    public override string ParametersJsonSchema => """{"type":"object","properties":{}}""";

    protected override ValueTask<object> ExecuteAsync(DiscoverProjectRootToolArguments arguments, ToolExecutionContext context, CancellationToken cancellationToken) =>
        ValueTask.FromResult<object>(new { root = PathSafety.DiscoverRoot(context.WorkingDirectory) });
}
