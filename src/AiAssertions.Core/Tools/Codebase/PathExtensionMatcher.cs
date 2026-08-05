namespace AiAssertions.Core.Tools.Codebase;

internal static class PathExtensionMatcher
{
    internal static bool Matches(string path, string? extension) =>
        string.IsNullOrWhiteSpace(extension)
        || Path.GetExtension(path).Equals(Normalize(extension), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string extension) =>
        extension.StartsWith('.') ? extension : $".{extension}";
}
