using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AiAssertions.Core.Tools.Codebase;

internal sealed class CodebaseFileIndex
{
    private readonly string _root;
    private readonly object _syncRoot = new();
    private Task<IReadOnlyList<string>>? _files;

    internal CodebaseFileIndex(string root) =>
        _root = Path.GetFullPath(root);

    internal async Task<IReadOnlyList<string>> GetFilesAsync(string requestedRoot, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(requestedRoot);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        if (!root.Equals(_root, comparison)
            && !root.StartsWith(_root + Path.DirectorySeparatorChar, comparison))
            throw new InvalidOperationException("The requested root is outside the codebase index root.");

        Task<IReadOnlyList<string>> files;
        lock (_syncRoot)
            files = _files ??= BuildAsync(cancellationToken);

        try
        {
            var indexedFiles = await files.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (root.Equals(_root, comparison))
                return indexedFiles;

            return indexedFiles
                .Where(path => path.StartsWith(root + Path.DirectorySeparatorChar, comparison))
                .ToArray();
        }
        catch
        {
            if (files.IsCanceled || files.IsFaulted)
                lock (_syncRoot)
                    if (ReferenceEquals(_files, files))
                        _files = null;

            throw;
        }
    }

    private async Task<IReadOnlyList<string>> BuildAsync(CancellationToken cancellationToken)
    {
        var gitFiles = await TryReadGitFilesAsync(cancellationToken).ConfigureAwait(false);
        if (gitFiles is not null)
            return gitFiles;

        var gitIgnore = GitIgnoreMatcher.Load(_root);
        var files = new List<string>();
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        foreach (var path in Directory.EnumerateFiles(_root, "*", options))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(_root, path);
            if (!PathSafety.IsIgnoredPath(relativePath) && !gitIgnore.IsIgnored(relativePath))
                files.Add(Path.GetFullPath(path));
        }

        files.Sort(GetPathComparer());
        return files;
    }

    private async Task<IReadOnlyList<string>?> TryReadGitFilesAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(Path.Combine(_root, ".git")) && !File.Exists(Path.Combine(_root, ".git")))
            return null;

        var trackedFiles = await TryRunGitLsFilesAsync(
                ["--cached", "--recurse-submodules", "-z"],
                cancellationToken)
            .ConfigureAwait(false);
        if (trackedFiles is null)
            return null;

        var untrackedFiles = await TryRunGitLsFilesAsync(
                ["--others", "--exclude-standard", "-z"],
                cancellationToken)
            .ConfigureAwait(false);
        if (untrackedFiles is null)
            return null;

        return trackedFiles
            .Concat(untrackedFiles)
            .Distinct(GetPathComparer())
            .Select(path => Path.GetFullPath(Path.Combine(_root, path)))
            .Where(File.Exists)
            .Where(path => !PathSafety.IsIgnoredPath(Path.GetRelativePath(_root, path)))
            .Order(GetPathComparer())
            .ToArray();
    }

    private async Task<IReadOnlyList<string>?> TryRunGitLsFilesAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        using var process = new Process();

        process.StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        process.StartInfo.ArgumentList.Add("-C");
        process.StartInfo.ArgumentList.Add(_root);
        process.StartInfo.ArgumentList.Add("ls-files");
        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);

        try
        {
            if (!process.Start())
                return null;

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync(cancellationToken)).ConfigureAwait(false);

            if (process.ExitCode != 0)
                return null;

            return outputTask.Result
                .Split('\0', StringSplitOptions.RemoveEmptyEntries);
        }
        catch (Win32Exception)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
    }

    private static StringComparer GetPathComparer() =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

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

    private sealed class GitIgnoreMatcher
    {
        private readonly IReadOnlyList<Rule> _rules;

        private GitIgnoreMatcher(IReadOnlyList<Rule> rules) =>
            _rules = rules;

        internal static GitIgnoreMatcher Load(string root)
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            };
            var ignoreFiles = Directory
                .EnumerateFiles(root, ".gitignore", options)
                .Select(path => new
                {
                    Path = path,
                    RelativePath = Path.GetRelativePath(root, path).Replace('\\', '/')
                })
                .Where(item => !PathSafety.IsIgnoredPath(item.RelativePath))
                .OrderBy(item => item.RelativePath.Count(character => character == '/'))
                .ThenBy(item => item.RelativePath, StringComparer.Ordinal)
                .ToArray();
            var rules = new List<Rule>();

            foreach (var ignoreFile in ignoreFiles)
            {
                if (rules.Count > 0 && new GitIgnoreMatcher(rules).IsIgnored(ignoreFile.RelativePath))
                    continue;

                var baseDirectory = Path.GetDirectoryName(ignoreFile.RelativePath)?.Replace('\\', '/') ?? string.Empty;
                foreach (var rawLine in File.ReadLines(ignoreFile.Path))
                {
                    var line = rawLine.TrimEnd();
                    if (line.Length == 0)
                        continue;

                    if (line.StartsWith("\\#", StringComparison.Ordinal))
                        line = line[1..];
                    else if (line.StartsWith('#'))
                        continue;

                    var effectivePattern = line.StartsWith('!') ? line[1..] : line;
                    if (effectivePattern.Trim('/').Length == 0)
                        continue;

                    rules.Add(Rule.Create(baseDirectory, line));
                }
            }

            return new GitIgnoreMatcher(rules);
        }

        internal bool IsIgnored(string relativePath)
        {
            var normalizedPath = relativePath.Replace('\\', '/');
            var ignored = false;

            foreach (var rule in _rules)
                if (rule.Regex.IsMatch(normalizedPath))
                    ignored = !rule.Negated;

            return ignored;
        }

        private sealed record Rule(bool Negated, Regex Regex)
        {
            internal static Rule Create(string baseDirectory, string pattern)
            {
                var negated = pattern.StartsWith('!');
                if (negated)
                    pattern = pattern[1..];
                else if (pattern.StartsWith("\\!", StringComparison.Ordinal))
                    pattern = pattern[1..];

                var anchored = pattern.StartsWith('/');
                if (anchored)
                    pattern = pattern[1..];

                var directoryOnly = pattern.EndsWith('/');
                pattern = pattern.TrimEnd('/');
                var hasSlash = pattern.Contains('/');
                var escapedBase = baseDirectory.Length == 0
                    ? string.Empty
                    : Regex.Escape(baseDirectory) + "/";
                var prefix = anchored || hasSlash
                    ? "^" + escapedBase
                    : "^" + escapedBase + "(?:.*/)?";
                var suffix = directoryOnly ? "/.*$" : "(?:/.*)?$";
                var options = RegexOptions.CultureInvariant;
                if (OperatingSystem.IsWindows())
                    options |= RegexOptions.IgnoreCase;

                return new Rule(
                    negated,
                    new Regex(prefix + ConvertPattern(pattern) + suffix, options, TimeSpan.FromMilliseconds(100)));
            }

            private static string ConvertPattern(string pattern)
            {
                var expression = new System.Text.StringBuilder(pattern.Length * 2);

                for (var index = 0; index < pattern.Length; index++)
                {
                    var character = pattern[index];
                    if (character == '\\' && index + 1 < pattern.Length)
                    {
                        expression.Append(Regex.Escape(pattern[++index].ToString()));
                        continue;
                    }

                    if (character == '*')
                    {
                        if (index + 1 < pattern.Length && pattern[index + 1] == '*')
                        {
                            index++;
                            if (index + 1 < pattern.Length && pattern[index + 1] == '/')
                            {
                                index++;
                                expression.Append("(?:.*/)?");
                            }
                            else
                            {
                                expression.Append(".*");
                            }
                        }
                        else
                        {
                            expression.Append("[^/]*");
                        }

                        continue;
                    }

                    if (character == '[')
                    {
                        var closingBracket = pattern.IndexOf(']', index + 1);
                        if (closingBracket > index + 1)
                        {
                            var characterClass = pattern[(index + 1)..closingBracket];
                            expression.Append('[');
                            if (characterClass.StartsWith('!'))
                            {
                                expression.Append('^');
                                characterClass = characterClass[1..];
                            }

                            foreach (var classCharacter in characterClass)
                                expression.Append(classCharacter switch
                                {
                                    '\\' => @"\\",
                                    ']' => @"\]",
                                    '^' => @"\^",
                                    _ => classCharacter.ToString()
                                });

                            expression.Append(']');
                            index = closingBracket;
                            continue;
                        }
                    }

                    expression.Append(character == '?' ? "[^/]" : Regex.Escape(character.ToString()));
                }

                return expression.ToString();
            }
        }
    }
}
