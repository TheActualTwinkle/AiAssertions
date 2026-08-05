using System.Text.RegularExpressions;

namespace AiAssertions.Core.Tools.Codebase;

internal sealed class GitIgnoreMatcher
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
