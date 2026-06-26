using System.Text.Json.Serialization;

namespace AiAssertions.Core.Assertions;

internal sealed record AiAssertionEvidence
{
    [JsonPropertyName("file")]
    public string File { get; init; } = string.Empty;

    [JsonPropertyName("start_line")]
    public int StartLine { get; init; }

    [JsonPropertyName("end_line")]
    public int EndLine { get; init; }

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;
}
