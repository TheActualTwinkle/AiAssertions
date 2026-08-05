namespace AiAssertions.Tests;

internal sealed class CodebaseToolsTestTemporaryDirectory : IDisposable
{
    internal CodebaseToolsTestTemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"AiAssertions.Tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    internal string Path { get; }

    internal string File(string relativePath) =>
        System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));

    public void Dispose() =>
        Directory.Delete(Path, recursive: true);
}
