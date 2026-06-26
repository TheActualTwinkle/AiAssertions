namespace AiAssertions.Core.Tools.Codebase;

internal sealed class FindFilesByNameToolArguments
{
    public string? Root { get; init; }

    public string? Name { get; init; }

    public int? MaxResults { get; init; }
}
