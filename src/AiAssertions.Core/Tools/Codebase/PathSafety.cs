using AiAssertions.Core.Tools.Abstractions;

namespace AiAssertions.Core.Tools.Codebase;

internal static class PathSafety
{
    internal static string ResolveRoot(ToolExecutionContext context, string? requestedRoot)
    {
        var allowedRoot = DiscoverRoot(context.WorkingDirectory);

        if (string.IsNullOrWhiteSpace(requestedRoot))
            return allowedRoot;

        var fullRoot = Path.GetFullPath(requestedRoot);
        var comparison = GetPathComparison();

        if (!IsInside(fullRoot, allowedRoot, comparison)
            || !IsInside(ResolvePhysicalPath(fullRoot), ResolvePhysicalPath(allowedRoot), comparison))
            throw new InvalidOperationException("The requested root is outside the discovered project root.");

        return fullRoot;
    }

    internal static string ResolveInsideRoot(string root, string relativePath)
    {
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        var comparison = GetPathComparison();

        if (!IsInside(fullPath, fullRoot, comparison)
            || !IsInside(ResolvePhysicalPath(fullPath), ResolvePhysicalPath(fullRoot), comparison))
            throw new InvalidOperationException("The requested path is outside the project root.");

        return fullPath;
    }

    internal static string DiscoverRoot(string workingDirectory)
    {
        var directory = new DirectoryInfo(workingDirectory);

        while (directory is not null)
        {
            var hasSolutionMarker = directory.EnumerateFiles("*.sln", SearchOption.TopDirectoryOnly).Any()
                || directory.EnumerateFiles("*.slnx", SearchOption.TopDirectoryOnly).Any();

            if (hasSolutionMarker)
                return directory.FullName;

            directory = directory.Parent;
        }

        directory = new DirectoryInfo(workingDirectory);

        while (directory is not null)
        {
            var hasRootMarker = directory.EnumerateFiles("*.csproj", SearchOption.TopDirectoryOnly).Any()
                || Directory.Exists(Path.Combine(directory.FullName, ".git"));

            if (hasRootMarker)
                return directory.FullName;

            directory = directory.Parent;
        }

        return Path.GetFullPath(workingDirectory);
    }

    internal static bool IsIgnoredPath(string relativePath) =>
        relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => IgnoredDirectoryNames.Contains(segment));

    private static bool IsInside(string path, string root, StringComparison comparison) =>
        path.Equals(root, comparison)
        || path.StartsWith(root + Path.DirectorySeparatorChar, comparison);

    private static string ResolvePhysicalPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var pathRoot = Path.GetPathRoot(fullPath)
            ?? throw new InvalidOperationException("Could not determine the path root.");
        var relativePath = Path.GetRelativePath(pathRoot, fullPath);
        if (relativePath == ".")
            return pathRoot;

        var current = pathRoot;
        foreach (var segment in relativePath.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            FileSystemInfo fileSystemInfo = Directory.Exists(current)
                ? new DirectoryInfo(current)
                : new FileInfo(current);

            if (!fileSystemInfo.Exists || (fileSystemInfo.Attributes & FileAttributes.ReparsePoint) == 0)
                continue;

            var target = fileSystemInfo.ResolveLinkTarget(returnFinalTarget: true);
            if (target is not null)
                current = target.FullName;
        }

        return Path.GetFullPath(current);
    }

    private static StringComparison GetPathComparison() =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static readonly HashSet<string> IgnoredDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".idea",
        ".vs",
        ".vscode",
        "bin",
        "obj",
        "node_modules"
    };
}
