using System.Text.Json.Serialization;

namespace AiAssertions.Core.Tools.Codebase;

internal sealed class ListProjectsToolArguments
{
    [JsonPropertyName("root")]
    public string? Root { get; init; }
}
