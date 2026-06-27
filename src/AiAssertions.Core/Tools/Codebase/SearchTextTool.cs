using System.Text;
using AiAssertions.Core.Tools.Abstractions;

namespace AiAssertions.Core.Tools.Codebase;

internal sealed class SearchTextTool : JsonTool<SearchTextToolArguments>
{
    public override string Name => "search_text";

    public override string Description => "Searches text in files and returns matching file paths and line numbers.";

    public override string ParametersJsonSchema => """{"type":"object","required":["query"],"properties":{"root":{"type":"string"},"query":{"type":"string"},"extension":{"type":"string"},"max_results":{"type":"integer"}}}""";

    protected override async ValueTask<object> ExecuteAsync(SearchTextToolArguments arguments, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(arguments.Query);
        
        var root = PathSafety.ResolveRoot(context, arguments.Root);
        var max = Math.Clamp(arguments.MaxResults ?? 50, 1, 200);
        var matches = new List<object>();

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (matches.Count >= max)
                break;

            if (PathSafety.IsIgnoredPath(file)
                || (!string.IsNullOrWhiteSpace(arguments.Extension) && !Path.GetExtension(file).Equals(arguments.Extension, StringComparison.OrdinalIgnoreCase)))
                continue;

            string[] lines;
            
            try
            {
                lines = await File.ReadAllLinesAsync(file, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                continue;
            }

            for (var index = 0; index < lines.Length && matches.Count < max; index++)
                if (lines[index].Contains(arguments.Query, StringComparison.OrdinalIgnoreCase))
                    matches.Add(new
                    {
                        file = Path.GetRelativePath(root, file),
                        line = index + 1,
                        text = lines[index].Trim()
                    });
        }

        return new { matches };
    }
}
