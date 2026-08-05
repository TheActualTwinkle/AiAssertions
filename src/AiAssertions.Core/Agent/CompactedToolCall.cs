using System.Text.Json.Serialization;

namespace AiAssertions.Core.Agent;

internal sealed class CompactedToolCall
{
    [JsonPropertyName("tool")]
    public string? Tool { get; init; }

    [JsonPropertyName("arguments")]
    public object? Arguments { get; init; }

    [JsonPropertyName("result_summary")]
    public string ResultSummary { get; set; } = string.Empty;
}
