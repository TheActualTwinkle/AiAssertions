using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiAssertions;

/// <summary>
/// Represents the complete execution trace of an AI codebase assertion.
/// </summary>
/// <remarks>
/// Trace payloads can contain prompts, source code, tool results, and provider errors. Store or publish them only in
/// locations appropriate for potentially sensitive codebase data.
/// </remarks>
public sealed record CodebaseAssertionExecutionTrace
{
    /// <summary>
    /// Gets the UTC time at which trace collection started.
    /// </summary>
    public required DateTimeOffset StartedAtUtc { get; init; }

    /// <summary>
    /// Gets the UTC time at which trace collection completed.
    /// </summary>
    public required DateTimeOffset CompletedAtUtc { get; init; }

    /// <summary>
    /// Gets the total duration of the assertion run.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the chronologically ordered trace entries.
    /// </summary>
    public required IReadOnlyList<CodebaseAssertionExecutionTraceEntry> Entries { get; init; }

    /// <summary>
    /// Serializes this execution trace to JSON.
    /// </summary>
    /// <param name="indented">Whether the resulting JSON should be indented. Defaults to <see langword="true"/>.</param>
    /// <returns>The serialized execution trace.</returns>
    public string ToJson(bool indented = true) =>
        JsonSerializer.Serialize(this, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = indented,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        });
}
