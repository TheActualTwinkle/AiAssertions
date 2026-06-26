using AiAssertions.Core.Tools.Abstractions;

namespace AiAssertions.Core.Tools.Codebase;

internal sealed class SearchFilesTool : JsonTool<SearchFilesToolArguments>
{
    public override string Name => "search_files";

    public override string Description => "Lists files under a root, optionally filtered by extension.";

    public override string ParametersJsonSchema => """{"type":"object","properties":{"root":{"type":"string"},"extension":{"type":"string","description":"Optional extension such as .cs"},"max_results":{"type":"integer"}}}""";

    protected override ValueTask<object> ExecuteAsync(SearchFilesToolArguments arguments, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        var root = PathSafety.ResolveRoot(context, arguments.Root);
        var max = Math.Clamp(arguments.MaxResults ?? 100, 1, 500);
        var extension = arguments.Extension;
        
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => string.IsNullOrWhiteSpace(extension) || Path.GetExtension(path).Equals(extension, StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(root, path))
            .Order(StringComparer.Ordinal)
            .Take(max)
            .ToArray();

        return ValueTask.FromResult<object>(new { files });
    }
}
