using System.Text.Json.Serialization;

namespace AiAssertions.Core.Tools.Codebase;

internal sealed class FindFilesByNameToolArguments
{
    [JsonPropertyName("root")]
    public string? Root { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("max_results")]
    public int? MaxResults { get; init; }
}
