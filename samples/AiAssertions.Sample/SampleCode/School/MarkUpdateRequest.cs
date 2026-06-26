namespace AiAssertions.Sample.SampleCode.School;

internal sealed record MarkUpdateRequest
{
    public Guid StudentId { get; init; }

    public required string Subject { get; init; }

    public int Value { get; init; }
}
