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
        
        if (!fullRoot.Equals(allowedRoot, StringComparison.Ordinal)
            && !fullRoot.StartsWith(allowedRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("The requested root is outside the discovered project root.");

        return fullRoot;
    }

    internal static string ResolveInsideRoot(string root, string relativePath)
    {
        var fullRoot = Path.GetFullPath(root);
        
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        
        if (!fullPath.Equals(fullRoot, StringComparison.Ordinal)
            && !fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
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
}
