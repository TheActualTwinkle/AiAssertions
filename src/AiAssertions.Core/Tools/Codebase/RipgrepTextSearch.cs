using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace AiAssertions.Core.Tools.Codebase;

internal static class RipgrepTextSearch
{
    internal static async Task<IReadOnlyList<TextSearchMatch>?> TrySearchAsync(
        string root,
        string query,
        string? extension,
        string? path,
        string? glob,
        int maxResults,
        CancellationToken cancellationToken,
        string executable = "rg")
    {
        using var process = CreateProcess(root, query, path, executable);

        try
        {
            if (!process.Start())
                return null;
        }
        catch (Win32Exception)
        {
            return null;
        }

        var matches = new List<TextSearchMatch>();
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            while (await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (TryParseMatch(root, query, line, out var match)
                    && PathExtensionMatcher.Matches(match.File, extension)
                    && PathGlobMatcher.Matches(match.File, glob))
                    matches.Add(match);

                if (matches.Count < maxResults)
                    continue;

                TryKill(process);
                break;
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await errorTask.ConfigureAwait(false);
            return process.ExitCode is 0 or 1 || matches.Count > 0 ? matches : null;
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
    }

    private static Process CreateProcess(
        string root,
        string query,
        string? path,
        string executable)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = root,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.StartInfo.ArgumentList.Add("--json");
        process.StartInfo.ArgumentList.Add("--ignore-case");
        process.StartInfo.ArgumentList.Add("--hidden");
        process.StartInfo.ArgumentList.Add("--no-messages");
        process.StartInfo.ArgumentList.Add("--fixed-strings");
        process.StartInfo.ArgumentList.Add("--sort");
        process.StartInfo.ArgumentList.Add("path");

        foreach (var ignoredDirectory in IgnoredDirectories)
        {
            process.StartInfo.ArgumentList.Add("--glob");
            process.StartInfo.ArgumentList.Add($"!{ignoredDirectory}/**");
        }

        process.StartInfo.ArgumentList.Add("--");
        process.StartInfo.ArgumentList.Add(query);
        process.StartInfo.ArgumentList.Add(string.IsNullOrWhiteSpace(path) ? root : PathSafety.ResolveInsideRoot(root, path));
        return process;
    }

    private static bool TryParseMatch(string root, string query, string line, out TextSearchMatch match)
    {
        match = default;

        try
        {
            using var document = JsonDocument.Parse(line);
            var documentRoot = document.RootElement;
            if (documentRoot.GetProperty("type").GetString() != "match")
                return false;

            var data = documentRoot.GetProperty("data");
            var path = data.GetProperty("path").GetProperty("text").GetString();
            var text = data.GetProperty("lines").GetProperty("text").GetString();
            var lineNumber = data.GetProperty("line_number").GetInt32();
            if (string.IsNullOrWhiteSpace(path) || text is null)
                return false;

            var fullPath = Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(root, path));
            var matchIndex = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            var snippet = TextSearchSnippet.Create(text, Math.Max(matchIndex, 0), query.Length);

            match = new TextSearchMatch(
                Path.GetRelativePath(root, fullPath),
                lineNumber,
                snippet.Text,
                snippet.Truncated);
            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return false;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
        }
    }

    private static readonly string[] IgnoredDirectories =
    [
        ".git",
        ".idea",
        ".vs",
        ".vscode",
        "bin",
        "obj",
        "node_modules"
    ];
}

internal readonly record struct TextSearchMatch(string File, int Line, string Text, bool TextTruncated);
