namespace AiAssertions.Core.Tools.Codebase;

internal static class TextSearchSnippet
{
    private const int MaxChars = 500;

    internal static TextSearchSnippetResult Create(string line, int matchIndex, int matchLength)
    {
        if (line.Length <= MaxChars)
            return new TextSearchSnippetResult(line.Trim(), false);

        matchIndex = Math.Clamp(matchIndex, 0, line.Length);
        matchLength = Math.Clamp(matchLength, 0, line.Length - matchIndex);
        const string marker = "…";
        var contentBudget = MaxChars - (2 * marker.Length);
        var visibleMatchLength = Math.Min(matchLength, contentBudget);
        var start = matchIndex - ((contentBudget - visibleMatchLength) / 2);
        start = Math.Clamp(start, 0, line.Length - contentBudget);

        var matchEnd = matchIndex + matchLength;
        if (matchEnd > start + contentBudget)
            start = Math.Clamp(matchEnd - contentBudget, 0, line.Length - contentBudget);

        var end = Math.Min(line.Length, start + contentBudget);
        if (start > 0 && start < line.Length && char.IsLowSurrogate(line[start]))
            start++;
        if (end > start && end < line.Length && char.IsHighSurrogate(line[end - 1]))
            end--;

        var prefix = start > 0 ? marker : string.Empty;
        var suffix = end < line.Length ? marker : string.Empty;
        return new TextSearchSnippetResult(string.Concat(prefix, line.AsSpan(start, end - start), suffix), true);
    }
}

internal readonly record struct TextSearchSnippetResult(string Text, bool Truncated);
