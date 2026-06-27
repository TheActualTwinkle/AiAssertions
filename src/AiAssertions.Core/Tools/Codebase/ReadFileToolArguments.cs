using System.Text.Json.Serialization;

namespace AiAssertions.Core.Tools.Codebase;

internal sealed class ReadFileToolArguments
{
    [JsonPropertyName("root")]
    public string? Root { get; init; }

    [JsonPropertyName("path")]
    public string? Path { get; init; }

    [JsonPropertyName("start_line")]
    public int? StartLine { get; init; }

    [JsonPropertyName("line_count")]
    public int? LineCount { get; init; }
}
