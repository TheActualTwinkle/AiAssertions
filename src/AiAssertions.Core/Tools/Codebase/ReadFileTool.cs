using System.Text;
using AiAssertions.Core.Tools.Abstractions;

namespace AiAssertions.Core.Tools.Codebase;

internal sealed class ReadFileTool : JsonTool<ReadFileToolArguments>
{
    public override string Name => "read_file";

    public override string Description => "Reads a UTF-8 text file under the project root.";

    public override string ParametersJsonSchema => """{"type":"object","required":["path"],"properties":{"root":{"type":"string"},"path":{"type":"string"},"start_line":{"type":"integer"},"line_count":{"type":"integer"}}}""";

    protected override async ValueTask<object> ExecuteAsync(ReadFileToolArguments arguments, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(arguments.Path);
        
        var root = PathSafety.ResolveRoot(context, arguments.Root);
        var file = PathSafety.ResolveInsideRoot(root, arguments.Path);
        var lines = await File.ReadAllLinesAsync(file, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        var start = Math.Clamp(arguments.StartLine ?? 1, 1, Math.Max(lines.Length, 1));
        var count = Math.Clamp(arguments.LineCount ?? 120, 1, 400);
        
        var selected = lines.Skip(start - 1).Take(count).Select((text, index) => new
        {
            line = start + index,
            text
        }).ToArray();

        return new
        {
            file = arguments.Path,
            start_line = start,
            line_count = selected.Length,
            lines = selected
        };
    }
}
