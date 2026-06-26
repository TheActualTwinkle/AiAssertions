namespace AiAssertions.Core.Tools.Codebase;

internal sealed class ReadFileToolArguments
{
    public string? Root { get; init; }

    public string? Path { get; init; }

    public int? StartLine { get; init; }

    public int? LineCount { get; init; }
}
