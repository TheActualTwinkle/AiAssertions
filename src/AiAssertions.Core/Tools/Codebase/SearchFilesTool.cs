using AiAssertions.Core.Tools.Abstractions;

namespace AiAssertions.Core.Tools.Codebase;

internal sealed class SearchFilesTool : JsonTool<SearchFilesToolArguments>
{
    public override string Name => "search_files";

    public override string Description => "Lists a page of files under a root, optionally filtered by extension or glob. Files matched by .gitignore are excluded by default; set include_ignored=true only when they are explicitly relevant. Use next_offset while has_more is true to inspect every result.";

    public override string ParametersJsonSchema => """{"type":"object","properties":{"root":{"type":"string"},"extension":{"type":"string","description":"Optional extension such as .cs"},"glob":{"type":"string","description":"Optional path glob such as Source/**/*.cs"},"include_ignored":{"type":"boolean","description":"Include files matched by .gitignore. Defaults to false; enable only for targeted searches when ignored files are explicitly relevant."},"max_results":{"type":"integer"},"offset":{"type":"integer","description":"Zero-based result offset. Use next_offset from the previous response."}}}""";

    protected override async ValueTask<object> ExecuteAsync(SearchFilesToolArguments arguments, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        var root = PathSafety.ResolveRoot(context, arguments.Root);
        var max = Math.Clamp(arguments.MaxResults ?? 100, 1, 500);
        var offset = arguments.Offset ?? 0;
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(arguments.Offset), "Result offset must not be negative.");

        var indexedFiles = await context.FileIndex.GetFilesAsync(root, cancellationToken, arguments.IncludeIgnored).ConfigureAwait(false);
        var page = indexedFiles
            .Where(path => PathExtensionMatcher.Matches(path, arguments.Extension))
            .Select(path => PathSafety.GetPortableRelativePath(root, path))
            .Where(path => PathGlobMatcher.Matches(path, arguments.Glob))
            .Skip(offset)
            .Take(max + 1)
            .ToArray();
        var hasMore = page.Length > max;
        var files = page.Take(max).ToArray();

        return new
        {
            files,
            returned_count = files.Length,
            offset,
            has_more = hasMore,
            next_offset = hasMore ? offset + files.Length : (int?)null
        };
    }
}
