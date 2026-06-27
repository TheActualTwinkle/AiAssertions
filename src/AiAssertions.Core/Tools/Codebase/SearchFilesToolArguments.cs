using System.Text.Json.Serialization;

namespace AiAssertions.Core.Tools.Codebase;

internal sealed class SearchFilesToolArguments
{
    [JsonPropertyName("root")]
    public string? Root { get; init; }

    [JsonPropertyName("extension")]
    public string? Extension { get; init; }

    [JsonPropertyName("max_results")]
    public int? MaxResults { get; init; }
}
