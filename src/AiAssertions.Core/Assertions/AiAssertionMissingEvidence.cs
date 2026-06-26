using System.Text.Json.Serialization;

namespace AiAssertions.Core.Assertions;

internal sealed record AiAssertionMissingEvidence
{
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("expected_location")]
    public string? ExpectedLocation { get; init; }
}
