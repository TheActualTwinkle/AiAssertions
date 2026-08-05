namespace AiAssertions.Tests;

internal sealed class EngineTestTemporaryDirectory : IDisposable
{
    internal EngineTestTemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"AiAssertions.Tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    internal string Path { get; }

    public void Dispose() =>
        Directory.Delete(Path, recursive: true);
}
