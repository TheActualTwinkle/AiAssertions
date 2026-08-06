using AiAssertions.Core.Tools.Abstractions;

namespace AiAssertions.Core.Tools.Codebase;

internal sealed class FindFilesByNameTool : JsonTool<FindFilesByNameToolArguments>
{
    public override string Name => "find_files_by_name";

    public override string Description => "Finds a page of files whose names contain a case-insensitive value. Files matched by .gitignore are excluded by default; set include_ignored=true only when they are explicitly relevant. Use next_offset while has_more is true to inspect every result.";

    public override string ParametersJsonSchema => """{"type":"object","required":["name"],"properties":{"root":{"type":"string"},"name":{"type":"string"},"include_ignored":{"type":"boolean","description":"Include files matched by .gitignore. Defaults to false; enable only for targeted searches when ignored files are explicitly relevant."},"max_results":{"type":"integer"},"offset":{"type":"integer","description":"Zero-based result offset. Use next_offset from the previous response."}}}""";

    protected override async ValueTask<object> ExecuteAsync(FindFilesByNameToolArguments arguments, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(arguments.Name);

        var root = PathSafety.ResolveRoot(context, arguments.Root);

        var max = Math.Clamp(arguments.MaxResults ?? 50, 1, 200);
        var offset = arguments.Offset ?? 0;
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(arguments.Offset), "Result offset must not be negative.");

        var indexedFiles = await context.FileIndex.GetFilesAsync(root, cancellationToken, arguments.IncludeIgnored).ConfigureAwait(false);
        var page = indexedFiles
            .Where(path => Path.GetFileName(path).Contains(arguments.Name, StringComparison.OrdinalIgnoreCase))
            .Select(path => PathSafety.GetPortableRelativePath(root, path))
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
