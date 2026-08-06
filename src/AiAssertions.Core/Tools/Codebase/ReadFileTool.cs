using System.Text;
using AiAssertions.Core.Tools.Abstractions;

namespace AiAssertions.Core.Tools.Codebase;

internal sealed class ReadFileTool : JsonTool<ReadFileToolArguments>
{
    private const int MaxLineChars = 1_000;
    private const int MaxContentChars = 30_000;

    public override string Name => "read_file";

    public override string Description => "Reads a UTF-8 text file under the project root and returns bounded line-numbered content plus pagination metadata. Continue at next_start_line while has_more is true; content_truncated reports shortened long lines or a full output budget.";

    public override string ParametersJsonSchema => """{"type":"object","required":["path"],"properties":{"root":{"type":"string"},"path":{"type":"string"},"start_line":{"type":"integer","description":"One-based first line. Defaults to 1."},"line_count":{"type":"integer","description":"Number of lines to read. Defaults to 120 and is limited to 400."}}}""";

    protected override async ValueTask<object> ExecuteAsync(ReadFileToolArguments arguments, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(arguments.Path);

        var root = PathSafety.ResolveRoot(context, arguments.Root);
        var file = PathSafety.ResolveInsideRoot(root, arguments.Path);
        var start = Math.Max(arguments.StartLine ?? 1, 1);
        var count = Math.Clamp(arguments.LineCount ?? 120, 1, 400);
        var endExclusive = (long)start + count;
        var content = new StringBuilder();
        var totalLines = 0;
        var returnedLines = 0;
        var truncatedLines = 0;
        var contentBudgetReached = false;

        await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            totalLines++;
            if (totalLines < start || totalLines >= endExclusive || contentBudgetReached)
                continue;

            var renderedLine = TruncateLine(line, out var lineWasTruncated);
            var entry = $"{totalLines}: {renderedLine}";
            var separatorLength = content.Length == 0 ? 0 : Environment.NewLine.Length;
            if (content.Length + separatorLength + entry.Length > MaxContentChars)
            {
                contentBudgetReached = true;
                continue;
            }

            if (content.Length > 0)
                content.AppendLine();

            content.Append(entry);
            returnedLines++;
            if (lineWasTruncated)
                truncatedLines++;
        }

        var end = returnedLines == 0 ? start : start + returnedLines - 1;
        var hasMore = end < totalLines;

        return new
        {
            file = PathSafety.GetPortableRelativePath(root, file),
            start_line = start,
            end_line = end,
            line_count = returnedLines,
            total_lines = totalLines,
            has_more = hasMore,
            next_start_line = hasMore ? end + 1 : (int?)null,
            content_truncated = contentBudgetReached || truncatedLines > 0,
            truncated_line_count = truncatedLines,
            content = content.ToString()
        };
    }

    private static string TruncateLine(string line, out bool truncated)
    {
        truncated = line.Length > MaxLineChars;
        if (!truncated)
            return line;

        const string marker = "… [line truncated]";
        var prefixLength = MaxLineChars - marker.Length;
        if (prefixLength > 0 && char.IsHighSurrogate(line[prefixLength - 1]))
            prefixLength--;

        return string.Concat(line.AsSpan(0, prefixLength), marker);
    }
}
