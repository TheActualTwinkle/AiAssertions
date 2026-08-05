using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace AiAssertions.Core.Tools.Codebase;

internal static class PathGlobMatcher
{
    private static readonly ConcurrentDictionary<string, Regex> Patterns = new(StringComparer.Ordinal);

    internal static bool Matches(string path, string? glob)
    {
        if (string.IsNullOrWhiteSpace(glob))
            return true;

        var normalizedGlob = glob.Replace('\\', '/').TrimStart('/');
        var regex = Patterns.GetOrAdd(normalizedGlob, CreateRegex);
        return regex.IsMatch(path.Replace('\\', '/'));
    }

    private static Regex CreateRegex(string glob)
    {
        var expression = new StringBuilder(glob.Length * 2);
        expression.Append(glob.Contains('/') ? '^' : "(?:^|.*/)");

        for (var index = 0; index < glob.Length; index++)
        {
            var character = glob[index];
            if (character == '*')
            {
                if (index + 1 < glob.Length && glob[index + 1] == '*')
                {
                    index++;
                    if (index + 1 < glob.Length && glob[index + 1] == '/')
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

            expression.Append(character == '?' ? "[^/]" : Regex.Escape(character.ToString()));
        }

        expression.Append('$');
        var options = RegexOptions.CultureInvariant;
        if (OperatingSystem.IsWindows())
            options |= RegexOptions.IgnoreCase;

        return new Regex(expression.ToString(), options, TimeSpan.FromMilliseconds(100));
    }
}
