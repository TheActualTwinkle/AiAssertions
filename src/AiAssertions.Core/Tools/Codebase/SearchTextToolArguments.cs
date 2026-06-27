using System.Text.Json.Serialization;

namespace AiAssertions.Core.Tools.Codebase;

internal sealed class SearchTextToolArguments
{
    [JsonPropertyName("root")]
    public string? Root { get; init; }

    [JsonPropertyName("query")]
    public string? Query { get; init; }

    [JsonPropertyName("extension")]
    public string? Extension { get; init; }

    [JsonPropertyName("max_results")]
    public int? MaxResults { get; init; }
}
