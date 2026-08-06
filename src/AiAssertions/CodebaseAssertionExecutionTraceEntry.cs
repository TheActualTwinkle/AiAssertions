using System.Text.Json.Serialization;

namespace AiAssertions;

/// <summary>
/// Represents one chronologically ordered execution-trace entry.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(CodebaseAssertionModelExchangeTraceEntry), "modelExchange")]
[JsonDerivedType(typeof(CodebaseAssertionCompactionModelExchangeTraceEntry), "conversationCompactionModelExchange")]
[JsonDerivedType(typeof(CodebaseAssertionConversationCompactionTraceEntry), "conversationCompaction")]
[JsonDerivedType(typeof(CodebaseAssertionToolExecutionTraceEntry), "toolExecution")]
[JsonDerivedType(typeof(CodebaseAssertionRunCompletedTraceEntry), "runCompleted")]
public abstract record CodebaseAssertionExecutionTraceEntry
{
    /// <summary>
    /// Gets the sequence assigned when the operation started.
    /// </summary>
    public required int Sequence { get; init; }

    /// <summary>
    /// Gets the UTC time at which the operation started.
    /// </summary>
    public required DateTimeOffset StartedAtUtc { get; init; }

    /// <summary>
    /// Gets the elapsed duration of the operation.
    /// </summary>
    public required TimeSpan Duration { get; init; }
}
