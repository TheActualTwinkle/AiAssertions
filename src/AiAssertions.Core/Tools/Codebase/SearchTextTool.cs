using System.Text;
using System.Text.RegularExpressions;
using AiAssertions.Core.Tools.Abstractions;

namespace AiAssertions.Core.Tools.Codebase;

internal sealed class SearchTextTool : JsonTool<SearchTextToolArguments>
{
    public override string Name => "search_text";

    public override string Description => "Searches literal text by default and returns a page of matching paths, lines, and bounded snippets. Files matched by .gitignore are excluded by default; set include_ignored=true only when they are explicitly relevant. Set use_regex=true only for regular expressions. Use next_offset while has_more is true.";

    public override string ParametersJsonSchema => """{"type":"object","required":["query"],"properties":{"root":{"type":"string"},"query":{"type":"string","description":"Literal text unless use_regex is true."},"extension":{"type":"string","description":"Optional extension such as .cs"},"path":{"type":"string","description":"Optional relative file or directory to search within."},"glob":{"type":"string","description":"Optional path glob such as Source/**/*.cs"},"use_regex":{"type":"boolean","description":"Treat query as a .NET regular expression. Defaults to false."},"include_ignored":{"type":"boolean","description":"Include files matched by .gitignore. Defaults to false; enable only for targeted searches when ignored files are explicitly relevant."},"max_results":{"type":"integer"},"offset":{"type":"integer","description":"Zero-based match offset. Use next_offset from the previous response."}}}""";

    protected override async ValueTask<object> ExecuteAsync(SearchTextToolArguments arguments, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(arguments.Query);

        var root = PathSafety.ResolveRoot(context, arguments.Root);
        var max = Math.Clamp(arguments.MaxResults ?? 50, 1, 200);
        var offset = arguments.Offset ?? 0;
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(arguments.Offset), "Match offset must not be negative.");

        var requestedMatches = checked(offset + max + 1);
        var matches = new List<TextSearchMatch>();
        var regex = arguments.UseRegex
            ? new Regex(arguments.Query, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250))
            : null;
        var indexedFiles = await context.FileIndex
            .GetFilesAsync(root, cancellationToken, arguments.IncludeIgnored)
            .ConfigureAwait(false);
        var indexedRelativePaths = indexedFiles
            .Select(file => Path.GetRelativePath(root, file))
            .ToHashSet(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var ripgrepMatches = arguments.UseRegex
            ? null
            : await RipgrepTextSearch.TrySearchAsync(
                    root,
                    arguments.Query,
                    arguments.Extension,
                    arguments.Path,
                    arguments.Glob,
                    requestedMatches,
                    cancellationToken,
                    indexedRelativePaths)
                .ConfigureAwait(false);

        if (ripgrepMatches is not null)
            return CreateResult(ripgrepMatches, offset, max);

        var scopedPath = ResolveScopedPath(root, arguments.Path);

        foreach (var file in indexedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (matches.Count >= requestedMatches)
                break;

            var relativePath = Path.GetRelativePath(root, file);
            if (!IsInsideScope(file, scopedPath)
                || !PathGlobMatcher.Matches(relativePath, arguments.Glob)
                || !PathExtensionMatcher.Matches(file, arguments.Extension))
                continue;

            try
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true);
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                var lineNumber = 0;

                while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
                {
                    lineNumber++;
                    if (matches.Count >= requestedMatches)
                        break;

                    var regexMatch = regex?.Match(line);
                    var matchIndex = regexMatch?.Success == true
                        ? regexMatch.Index
                        : line.IndexOf(arguments.Query, StringComparison.OrdinalIgnoreCase);

                    if (matchIndex < 0)
                        continue;

                    var matchLength = regexMatch?.Success == true
                        ? regexMatch.Length
                        : arguments.Query.Length;
                    var snippet = TextSearchSnippet.Create(line, matchIndex, matchLength);
                    matches.Add(new TextSearchMatch(relativePath, lineNumber, snippet.Text, snippet.Truncated));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException)
            {
            }
        }

        return CreateResult(matches, offset, max);
    }

    private static object CreateResult(IReadOnlyList<TextSearchMatch> matches, int offset, int max)
    {
        var page = matches.Skip(offset).Take(max + 1).ToArray();
        var hasMore = page.Length > max;
        var returnedMatches = page.Take(max).ToArray();

        return new
        {
            matches = returnedMatches.Select(match => new
            {
                file = match.File,
                line = match.Line,
                text = match.Text,
                text_truncated = match.TextTruncated
            }),
            returned_count = returnedMatches.Length,
            offset,
            has_more = hasMore,
            next_offset = hasMore ? offset + returnedMatches.Length : (int?)null
        };
    }

    private static string? ResolveScopedPath(string root, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        return PathSafety.ResolveInsideRoot(root, path);
    }

    private static bool IsInsideScope(string file, string? scope)
    {
        if (scope is null)
            return true;

        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return file.Equals(scope, comparison)
            || file.StartsWith(scope + Path.DirectorySeparatorChar, comparison);
    }
}
