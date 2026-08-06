using System.Globalization;
using System.Text;
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

    private static readonly JsonSerializerOptions BestEffortOptions = new(Options)
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    internal static AiAssertionResult ParseVerdict(string content)
    {
        try
        {
            return ParseVerdictCore(content);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return ParseBestEffort(content, exception);
        }
    }

    private static AiAssertionResult ParseVerdictCore(string content)
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
            try
            {
                return ParseResult(EscapeInvalidJsonStringBackslashes(trimmed));
            }
            catch (JsonException)
            {
                var extracted = ExtractJson(content);

                try
                {
                    return ParseResult(extracted);
                }
                catch (JsonException)
                {
                    return ParseResult(EscapeInvalidJsonStringBackslashes(extracted));
                }
            }
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

    private static AiAssertionResult ParseBestEffort(string content, Exception parseException)
    {
        var evidence = ReadArrayBestEffort<AiAssertionEvidence>(content, "evidence")
            .Select(NormalizeEvidence)
            .ToArray();
        var missingEvidence = ReadArrayBestEffort<AiAssertionMissingEvidence>(content, "missing_evidence").ToList();
        var error = parseException.Message.ReplaceLineEndings(" ");
        if (error.Length > 500)
            error = error[..500];

        missingEvidence.Add(new AiAssertionMissingEvidence
        {
            Description = $"The model verdict JSON was malformed and was parsed best-effort: {error}",
            ExpectedLocation = "Model response JSON"
        });

        return new AiAssertionResult
        {
            Passed = TryReadBoolean(content, "passed", out var passed) && passed,
            Confidence = TryReadDouble(content, "confidence", out var confidence)
                ? Math.Clamp(confidence, 0, 1)
                : 0,
            IsConclusive = TryReadBoolean(content, "is_conclusive", out var isConclusive) && isConclusive,
            Reason = TryReadString(content, "reason", out var reason)
                ? reason
                : $"The model verdict JSON was malformed; available fields were parsed best-effort. {error}",
            Evidence = evidence,
            MissingEvidence = missingEvidence,
            ParsingError = error
        };
    }

    private static AiAssertionEvidence NormalizeEvidence(AiAssertionEvidence evidence)
    {
        var startLine = Math.Max(evidence.StartLine, 1);
        var endLine = Math.Max(evidence.EndLine, startLine);

        return evidence with
        {
            File = NormalizeModelPath(evidence.File),
            StartLine = startLine,
            EndLine = endLine
        };
    }

    private static string NormalizeModelPath(string path) => path
        // An unescaped Windows path may already have been decoded as JSON control escapes.
        .Replace("\b", "/b", StringComparison.Ordinal)
        .Replace("\f", "/f", StringComparison.Ordinal)
        .Replace("\n", "/n", StringComparison.Ordinal)
        .Replace("\r", "/r", StringComparison.Ordinal)
        .Replace("\t", "/t", StringComparison.Ordinal)
        .Replace('\\', '/');

    private static string ExtractJson(string content)
    {
        var codeBlockMatch = CodeBlockRegex().Match(content);

        if (codeBlockMatch.Success)
            return codeBlockMatch.Groups[1].Value.Trim();

        var passedProperty = MatchProperty(content, "passed", string.Empty);
        var openBraceIndex = passedProperty.Success
            ? content.LastIndexOf('{', passedProperty.Index)
            : content.IndexOf('{');

        if (openBraceIndex < 0
            || !TryFindCollectionEnd(content, openBraceIndex, '{', '}', out var closeBraceIndex))
            throw new InvalidOperationException("Could not extract valid JSON from the model response.");

        return content[openBraceIndex..(closeBraceIndex + 1)].Trim();
    }

    private static string EscapeInvalidJsonStringBackslashes(string json)
    {
        var builder = new StringBuilder(json.Length);
        var insideString = false;

        for (var index = 0; index < json.Length; index++)
        {
            var character = json[index];
            if (character == '"')
            {
                insideString = !insideString;
                builder.Append(character);
                continue;
            }

            if (!insideString || character != '\\')
            {
                builder.Append(character);
                continue;
            }

            if (index + 1 >= json.Length || !IsValidJsonEscape(json, index + 1))
            {
                builder.Append('\\');
                builder.Append(character);
                continue;
            }

            builder.Append(character);
            builder.Append(json[++index]);
        }

        return builder.ToString();
    }

    private static bool IsValidJsonEscape(string json, int escapedCharacterIndex)
    {
        var escapedCharacter = json[escapedCharacterIndex];
        if (escapedCharacter is '"' or '\\' or '/' or 'b' or 'f' or 'n' or 'r' or 't')
            return true;

        if (escapedCharacter != 'u' || escapedCharacterIndex + 4 >= json.Length)
            return false;

        return IsFourHexDigits(json.AsSpan(escapedCharacterIndex + 1, 4));
    }

    private static bool IsFourHexDigits(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (!char.IsAsciiHexDigit(character))
                return false;
        }

        return true;
    }

    private static bool TryReadBoolean(string content, string propertyName, out bool value)
    {
        var match = MatchProperty(content, propertyName, "(?<value>true|false)");
        return bool.TryParse(match.Groups["value"].Value, out value);
    }

    private static bool TryReadDouble(string content, string propertyName, out double value)
    {
        var match = MatchProperty(
            content,
            propertyName,
            "(?<value>[-+]?(?:[0-9]+(?:\\.[0-9]*)?|\\.[0-9]+)(?:[eE][-+]?[0-9]+)?)");
        return double.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadString(string content, string propertyName, out string value)
    {
        var match = MatchProperty(content, propertyName, "(?<value>\"(?:\\\\.|[^\"\\\\])*\")");
        if (!match.Success)
        {
            value = string.Empty;
            return false;
        }

        try
        {
            value = JsonSerializer.Deserialize<string>(
                        EscapeInvalidJsonStringBackslashes(match.Groups["value"].Value),
                        BestEffortOptions)
                    ?? string.Empty;
            return true;
        }
        catch (JsonException)
        {
            value = match.Groups["value"].Value.Trim('"');
            return true;
        }
    }

    private static IReadOnlyList<T> ReadArrayBestEffort<T>(string content, string propertyName)
    {
        var propertyMatch = MatchProperty(content, propertyName, string.Empty);
        if (!propertyMatch.Success)
            return [];

        var arrayStart = content.IndexOf('[', propertyMatch.Index + propertyMatch.Length);
        if (arrayStart < 0 || !TryFindCollectionEnd(content, arrayStart, '[', ']', out var arrayEnd))
            return [];

        var arrayJson = EscapeInvalidJsonStringBackslashes(content[arrayStart..(arrayEnd + 1)]);

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<T>>(arrayJson, BestEffortOptions) ?? [];
        }
        catch (JsonException)
        {
            var items = new List<T>();
            var index = 1;

            while (index < arrayJson.Length - 1)
            {
                var objectStart = arrayJson.IndexOf('{', index);
                if (objectStart < 0 || !TryFindCollectionEnd(arrayJson, objectStart, '{', '}', out var objectEnd))
                    break;

                try
                {
                    var item = JsonSerializer.Deserialize<T>(arrayJson[objectStart..(objectEnd + 1)], BestEffortOptions);
                    if (item is not null)
                        items.Add(item);
                }
                catch (JsonException)
                {
                    // Skip only the malformed item and continue recovering the remaining array.
                }

                index = objectEnd + 1;
            }

            return items;
        }
    }

    private static Match MatchProperty(string content, string propertyName, string valuePattern) => Regex.Match(
        content,
        $"\\\"{Regex.Escape(propertyName)}\\\"\\s*:\\s*{valuePattern}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static bool TryFindCollectionEnd(
        string content,
        int start,
        char openCharacter,
        char closeCharacter,
        out int end)
    {
        var depth = 0;
        var insideString = false;

        for (var index = start; index < content.Length; index++)
        {
            var character = content[index];
            if (insideString)
            {
                if (character == '\\' && index + 1 < content.Length)
                {
                    index++;
                    continue;
                }

                if (character == '"')
                    insideString = false;

                continue;
            }

            if (character == '"')
            {
                insideString = true;
                continue;
            }

            if (character == openCharacter)
                depth++;
            else if (character == closeCharacter && --depth == 0)
            {
                end = index;
                return true;
            }
        }

        end = -1;
        return false;
    }

    [GeneratedRegex(@"```(?:json)?\s*\n?(.*?)\n?```", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex CodeBlockRegex();
}
