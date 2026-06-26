using AiAssertions.Core.Tools.Abstractions;

namespace AiAssertions.Core.Tools.Codebase;

internal sealed class FindFilesByNameTool : JsonTool<FindFilesByNameToolArguments>
{
    public override string Name => "find_files_by_name";

    public override string Description => "Finds files whose names contain a case-insensitive value.";

    public override string ParametersJsonSchema => """{"type":"object","required":["name"],"properties":{"root":{"type":"string"},"name":{"type":"string"},"max_results":{"type":"integer"}}}""";

    protected override ValueTask<object> ExecuteAsync(FindFilesByNameToolArguments arguments, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(arguments.Name);
        
        var root = PathSafety.ResolveRoot(context, arguments.Root);
        
        var max = Math.Clamp(arguments.MaxResults ?? 50, 1, 200);
        
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => Path.GetFileName(path).Contains(arguments.Name, StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(root, path))
            .Order(StringComparer.Ordinal)
            .Take(max)
            .ToArray();

        return ValueTask.FromResult<object>(new { files });
    }
}
