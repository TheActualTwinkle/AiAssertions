namespace AiAssertions.Core.Tools.Codebase;

internal sealed class SearchTextToolArguments
{
    public string? Root { get; init; }

    public string? Query { get; init; }

    public string? Extension { get; init; }

    public int? MaxResults { get; init; }
}
