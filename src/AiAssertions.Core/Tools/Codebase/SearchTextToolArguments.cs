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

    [JsonPropertyName("path")]
    public string? Path { get; init; }

    [JsonPropertyName("glob")]
    public string? Glob { get; init; }

    [JsonPropertyName("use_regex")]
    public bool UseRegex { get; init; }

    [JsonPropertyName("include_ignored")]
    public bool IncludeIgnored { get; init; }

    [JsonPropertyName("max_results")]
    public int? MaxResults { get; init; }

    [JsonPropertyName("offset")]
    public int? Offset { get; init; }
}
