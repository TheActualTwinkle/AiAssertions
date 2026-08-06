using System.Text.Json.Serialization;

namespace AiAssertions.Core.Assertions;

internal sealed record AiAssertionResult
{
    [JsonPropertyName("passed")]
    public bool Passed { get; init; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; init; }

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;

    [JsonPropertyName("evidence")]
    public IReadOnlyList<AiAssertionEvidence> Evidence { get; init; } = [];

    [JsonPropertyName("missing_evidence")]
    public IReadOnlyList<AiAssertionMissingEvidence> MissingEvidence { get; init; } = [];

    [JsonPropertyName("is_conclusive")]
    public bool IsConclusive { get; init; } = true;

    [JsonIgnore]
    internal AiAssertionExecutionTrace? ExecutionTrace { get; init; }
}
