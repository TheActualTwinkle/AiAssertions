using System.ComponentModel;
using System.Diagnostics;

namespace AiAssertions.Core.Tools.Codebase;

internal sealed class CodebaseFileIndex
{
    private readonly string _root;
    private readonly Lock _syncRoot = new();
    private Task<IReadOnlyList<string>>? _files;
    private Task<IReadOnlyList<string>>? _filesIncludingIgnored;

    internal CodebaseFileIndex(string root) =>
        _root = Path.GetFullPath(root);

    internal async Task<IReadOnlyList<string>> GetFilesAsync(
        string requestedRoot,
        CancellationToken cancellationToken,
        bool includeIgnored = false)
    {
        var root = Path.GetFullPath(requestedRoot);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        if (!root.Equals(_root, comparison)
            && !root.StartsWith(_root + Path.DirectorySeparatorChar, comparison))
            throw new InvalidOperationException("The requested root is outside the codebase index root.");

        Task<IReadOnlyList<string>> files;
        lock (_syncRoot)
            files = includeIgnored
                ? _filesIncludingIgnored ??= BuildAsync(includeIgnored: true, cancellationToken)
                : _files ??= BuildAsync(includeIgnored: false, cancellationToken);

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
                    if (includeIgnored)
                    {
                        if (ReferenceEquals(_filesIncludingIgnored, files))
                            _filesIncludingIgnored = null;
                    }
                    else if (ReferenceEquals(_files, files))
                    {
                        _files = null;
                    }

            throw;
        }
    }

    private async Task<IReadOnlyList<string>> BuildAsync(bool includeIgnored, CancellationToken cancellationToken)
    {
        var gitFiles = await TryReadGitFilesAsync(includeIgnored, cancellationToken).ConfigureAwait(false);
        if (gitFiles is not null)
            return gitFiles;

        var gitIgnore = includeIgnored ? null : GitIgnoreMatcher.Load(_root);
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
            if (!PathSafety.IsIgnoredPath(relativePath) && gitIgnore?.IsIgnored(relativePath) != true)
                files.Add(Path.GetFullPath(path));
        }

        files.Sort(GetPathComparer());
        return files;
    }

    private async Task<IReadOnlyList<string>?> TryReadGitFilesAsync(
        bool includeIgnored,
        CancellationToken cancellationToken)
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
                includeIgnored ? ["--others", "-z"] : ["--others", "--exclude-standard", "-z"],
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

}
