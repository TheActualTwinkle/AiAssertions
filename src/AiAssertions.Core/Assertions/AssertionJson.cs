using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AiAssertions.Core.Assertions;

internal static partial class AssertionJson
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    internal static AiAssertionResult ParseVerdict(string content)
    {
        var trimmed = content.Trim();

        if (trimmed.StartsWith("```", StringComparison.Ordinal))
            trimmed = trimmed
                .Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("```", string.Empty, StringComparison.Ordinal)
                .Trim();

        try
        {
            return ParseResult(trimmed);
        }
        catch (JsonException)
        {
            var extracted = ExtractJson(content);

            return ParseResult(extracted);
        }
    }

    private static AiAssertionResult ParseResult(string json)
    {
        var result = JsonSerializer.Deserialize<AiAssertionResult>(json, Options);

        if (result is null)
            throw new InvalidOperationException("The model returned an empty assertion verdict.");

        if (string.IsNullOrWhiteSpace(result.Reason))
            throw new InvalidOperationException("The model verdict must include a reason.");

        return result with
        {
            Confidence = Math.Clamp(result.Confidence, 0, 1),
            Evidence = result.Evidence.Select(NormalizeEvidence).ToArray(),
            MissingEvidence = result.MissingEvidence
        };
    }

    private static AiAssertionEvidence NormalizeEvidence(AiAssertionEvidence evidence)
    {
        var startLine = Math.Max(evidence.StartLine, 1);
        var endLine = Math.Max(evidence.EndLine, startLine);

        return evidence with
        {
            StartLine = startLine,
            EndLine = endLine
        };
    }

    private static string ExtractJson(string content)
    {
        var codeBlockMatch = CodeBlockRegex().Match(content);

        if (codeBlockMatch.Success)
            return codeBlockMatch.Groups[1].Value.Trim();

        var lastBraceIndex = content.LastIndexOf('{');

        if (lastBraceIndex < 0)
            throw new InvalidOperationException("Could not extract valid JSON from the model response.");

        var potentialJson = content[lastBraceIndex..];
        var closeBraceIndex = potentialJson.LastIndexOf('}');

        if (closeBraceIndex >= 0)
            return potentialJson[..(closeBraceIndex + 1)].Trim();

        throw new InvalidOperationException("Could not extract valid JSON from the model response.");
    }

    [GeneratedRegex(@"```(?:json)?\s*\n?(.*?)\n?```", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex CodeBlockRegex();
}
