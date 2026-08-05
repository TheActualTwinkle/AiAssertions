using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiAssertions.Core.Assertions;
using AiAssertions.Core.Models;

namespace AiAssertions.Core.Agent;

internal static class CodebaseConversationCompactor
{
    internal static IReadOnlyList<AiChatMessage> BuildRequestMessages(
        IReadOnlyList<AiChatMessage> messages,
        Func<IReadOnlyList<AiChatMessage>, IReadOnlyList<AiChatMessage>>? customCompactor,
        bool compactionEnabled,
        int recentToolCallTurns,
        int maxCompactedToolResultChars,
        int maxCompactedStateChars,
        int? maxRequestTokens,
        Func<IReadOnlyList<AiChatMessage>, int>? tokenEstimator)
    {
        var requestMessages = customCompactor is not null
            ? customCompactor(messages)
            : compactionEnabled
                ? BuildCompactedRequestMessages(
                    messages,
                    recentToolCallTurns,
                    maxCompactedToolResultChars,
                    maxCompactedStateChars)
                : messages;

        return maxRequestTokens is > 0
            ? ApplyTokenLimit(requestMessages, maxRequestTokens.Value, tokenEstimator, maxCompactedToolResultChars)
            : requestMessages;
    }

    private static IReadOnlyList<AiChatMessage> BuildCompactedRequestMessages(
        IReadOnlyList<AiChatMessage> messages,
        int recentToolCallTurns,
        int maxCompactedToolResultChars,
        int maxCompactedStateChars)
    {
        if (messages.Count <= 2)
            return messages;

        var recentStart = FindRecentStartIndex(messages, Math.Max(recentToolCallTurns, 1));
        var requestMessages = new List<AiChatMessage>(messages.Count - recentStart + 3)
        {
            messages[0],
            messages[1]
        };

        if (recentStart > 2)
        {
            var compacted = BuildCompactedState(
                messages.Skip(2).Take(recentStart - 2),
                maxCompactedToolResultChars,
                maxCompactedStateChars);

            if (!string.IsNullOrWhiteSpace(compacted))
                requestMessages.Add(new AiChatMessage
                {
                    Role = "user",
                    Content = compacted
                });
        }

        // The most recent tool turn is live evidence. Keep it byte-for-byte intact unless
        // the caller explicitly configured a request token limit, in which case
        // ApplyTokenLimit is responsible for shrinking it as a last resort.
        requestMessages.AddRange(messages.Skip(recentStart));

        return requestMessages;
    }

    private static IReadOnlyList<AiChatMessage> ApplyTokenLimit(
        IReadOnlyList<AiChatMessage> messages,
        int maxRequestTokens,
        Func<IReadOnlyList<AiChatMessage>, int>? tokenEstimator,
        int maxToolResultChars)
    {
        var estimateTokens = tokenEstimator ?? EstimateTokens;

        if (estimateTokens(messages) <= maxRequestTokens || messages.Count <= 2)
            return messages;

        var mandatoryMessages = messages.Take(2).ToArray();
        var mandatoryTokens = estimateTokens(mandatoryMessages);

        if (mandatoryTokens >= maxRequestTokens)
            return mandatoryMessages;

        var groups = BuildMessageGroups(messages.Skip(2)).ToArray();
        if (groups.Length == 0)
            return mandatoryMessages;

        var selectedGroups = new Stack<IReadOnlyList<AiChatMessage>>();
        var newestGroup = groups[^1];
        var newestCandidate = mandatoryMessages.Concat(newestGroup).ToArray();

        if (estimateTokens(newestCandidate) > maxRequestTokens)
        {
            newestCandidate = ShrinkToolResultsToFit(
                    newestCandidate,
                    maxRequestTokens,
                    estimateTokens,
                    maxToolResultChars)
                .ToArray();

            if (estimateTokens(newestCandidate) > maxRequestTokens)
                return mandatoryMessages;

            newestGroup = newestCandidate.Skip(mandatoryMessages.Length).ToArray();
        }

        selectedGroups.Push(newestGroup);
        var omittedMessages = groups
            .Take(groups.Length - 1)
            .Sum(group => group.Count);

        for (var index = groups.Length - 2; index >= 0; index--)
        {
            var group = groups[index];
            var candidate = mandatoryMessages
                .Concat(group)
                .Concat(selectedGroups.SelectMany(selectedGroup => selectedGroup))
                .ToArray();

            if (estimateTokens(candidate) <= maxRequestTokens)
            {
                selectedGroups.Push(group);
                omittedMessages -= group.Count;
            }
            else
            {
                break;
            }
        }

        var omittedNotice = new AiChatMessage
        {
            Role = "user",
            Content = $"Earlier conversation history was omitted to stay within the configured request token limit. Omitted messages: {omittedMessages}."
        };

        var result = new List<AiChatMessage>(messages.Count)
        {
            mandatoryMessages[0],
            mandatoryMessages[1]
        };

        var resultWithOmittedNotice = result
            .Append(omittedNotice)
            .Concat(selectedGroups.SelectMany(group => group))
            .ToArray();

        if (omittedMessages > 0 && estimateTokens(resultWithOmittedNotice) <= maxRequestTokens)
            result.Add(omittedNotice);

        foreach (var group in selectedGroups)
            result.AddRange(group);

        return result;
    }

    private static IEnumerable<IReadOnlyList<AiChatMessage>> BuildMessageGroups(IEnumerable<AiChatMessage> messages)
    {
        var pending = new List<AiChatMessage>();

        foreach (var message in messages)
        {
            if (message is { Role: "assistant", ToolCalls.Count: > 0 })
            {
                if (pending.Count > 0)
                {
                    yield return pending.ToArray();
                    pending.Clear();
                }

                pending.Add(message);
                continue;
            }

            switch (pending.Count)
            {
                case > 0 when message.Role == "tool":
                    pending.Add(message);
                    continue;
                case > 0:
                    yield return pending.ToArray();
                    pending.Clear();

                    break;
            }

            yield return [message];
        }

        if (pending.Count > 0)
            yield return pending.ToArray();
    }

    private static int EstimateTokens(IReadOnlyList<AiChatMessage> messages) =>
        messages.Sum(EstimateTokens);

    private static int EstimateTokens(AiChatMessage message)
    {
        var chars = message.Role.Length + message.Content.Length + (message.Name?.Length ?? 0) + (message.ToolCallId?.Length ?? 0);

        if (message.ToolCalls is null)
            return Math.Max(1, (int)Math.Ceiling(chars / 4d));

        foreach (var toolCall in message.ToolCalls)
            chars += toolCall.Id.Length + toolCall.Name.Length + toolCall.ArgumentsJson.Length;

        return Math.Max(1, (int)Math.Ceiling(chars / 4d));
    }

    private static int FindRecentStartIndex(IReadOnlyList<AiChatMessage> messages, int recentToolCallTurns)
    {
        var turns = 0;

        for (var index = messages.Count - 1; index >= 2; index--)
        {
            var message = messages[index];

            if (message.Role != "assistant" || message.ToolCalls is not { Count: > 0 })
                continue;

            turns++;

            if (turns >= recentToolCallTurns)
                return index;
        }

        return 2;
    }

    private static string BuildCompactedState(
        IEnumerable<AiChatMessage> compactedMessages,
        int maxToolResultChars,
        int maxCompactedStateChars)
    {
        var entries = new List<CompactedToolCall>();
        AiChatMessage? currentAssistant = null;

        foreach (var message in compactedMessages)
        {
            if (message is { Role: "assistant", ToolCalls.Count: > 0 })
            {
                currentAssistant = message;
                continue;
            }

            if (message.Role != "tool")
                continue;

            var toolCall = currentAssistant?.ToolCalls?.FirstOrDefault(call => call.Id == message.ToolCallId);
            entries.Add(new CompactedToolCall
            {
                Tool = message.Name,
                Arguments = TryReadJson(toolCall?.ArgumentsJson),
                ResultSummary = SummarizeToolResult(message.Content, maxToolResultChars)
            });
        }

        if (entries.Count == 0)
            return string.Empty;

        var omittedToolCalls = 0;
        var state = SerializeCompactedState(entries, omittedToolCalls);

        for (var index = 0; state.Length > maxCompactedStateChars && index < entries.Count; index++)
        {
            entries[index].ResultSummary = "Result omitted to stay within the compacted state budget.";
            state = SerializeCompactedState(entries, omittedToolCalls);
        }

        while (state.Length > maxCompactedStateChars && entries.Count > 0)
        {
            entries.RemoveAt(0);
            omittedToolCalls++;
            state = SerializeCompactedState(entries, omittedToolCalls);
        }

        if (state.Length > maxCompactedStateChars)
            return string.Empty;

        var builder = new StringBuilder();
        builder.AppendLine("Compacted assertion state from earlier tool calls:");
        builder.AppendLine("```json");
        builder.AppendLine(state);
        builder.AppendLine("```");
        builder.AppendLine("Continue from this state. Do not repeat completed searches or file reads unless the earlier summary is insufficient.");

        return builder.ToString();
    }

    private static object? TryReadJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<object>(json, AssertionJson.Options);
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static string SummarizeToolResult(string content, int maxChars)
    {
        if (content.Length <= maxChars)
            return content;

        const string marker = "... [truncated]";
        if (maxChars <= marker.Length)
            return marker[..maxChars];

        var prefixLength = maxChars - marker.Length;
        if (prefixLength > 0 && char.IsHighSurrogate(content[prefixLength - 1]))
            prefixLength--;

        return string.Concat(content.AsSpan(0, prefixLength), marker);
    }

    private static IReadOnlyList<AiChatMessage> ShrinkToolResultsToFit(
        IReadOnlyList<AiChatMessage> messages,
        int maxRequestTokens,
        Func<IReadOnlyList<AiChatMessage>, int> estimateTokens,
        int maxToolResultChars)
    {
        var result = messages.ToArray();
        var originalContents = result
            .Select(message => message.Content)
            .ToArray();

        for (var attempt = 0; attempt < 64 && estimateTokens(result) > maxRequestTokens; attempt++)
        {
            var candidate = result
                .Select((message, index) => (message, index))
                .Where(item => item.message.Role == "tool" && item.message.Content.Length > 32)
                .OrderByDescending(item => item.message.Content.Length)
                .FirstOrDefault();

            if (candidate.message is null)
                break;

            var currentLength = candidate.message.Content.Length;
            var targetLength = Math.Max(32, Math.Min(maxToolResultChars, currentLength / 2));
            if (targetLength >= currentLength)
                targetLength = Math.Max(32, currentLength - 32);

            result[candidate.index] = candidate.message with
            {
                Content = BuildTruncatedToolResult(originalContents[candidate.index], targetLength)
            };
        }

        return result;
    }

    private static string BuildTruncatedToolResult(string content, int maxChars)
    {
        if (content.Length <= maxChars)
            return content;

        var emptyPayload = SerializeTruncatedToolResult(content.Length, string.Empty);
        if (emptyPayload.Length > maxChars)
            return "{\"truncated\":true}";

        var low = 0;
        var high = Math.Min(content.Length, maxChars);
        var best = emptyPayload;

        while (low <= high)
        {
            var probeLength = low + ((high - low) / 2);
            var prefixLength = probeLength;
            if (prefixLength > 0 && char.IsHighSurrogate(content[prefixLength - 1]))
                prefixLength--;

            var candidate = SerializeTruncatedToolResult(content.Length, content[..prefixLength]);
            if (candidate.Length <= maxChars)
            {
                best = candidate;
                low = probeLength + 1;
            }
            else
            {
                high = probeLength - 1;
            }
        }

        return best;
    }

    private static string SerializeTruncatedToolResult(int originalChars, string contentPrefix) =>
        JsonSerializer.Serialize(
            new
            {
                truncated = true,
                original_chars = originalChars,
                content_prefix = contentPrefix
            },
            AssertionJson.Options);

    private static string SerializeCompactedState(IReadOnlyList<CompactedToolCall> entries, int omittedToolCalls) =>
        JsonSerializer.Serialize(
            new
            {
                completed_tool_calls = entries,
                omitted_tool_calls = omittedToolCalls
            },
            AssertionJson.Options);

    private sealed class CompactedToolCall
    {
        [JsonPropertyName("tool")]
        public string? Tool { get; init; }

        [JsonPropertyName("arguments")]
        public object? Arguments { get; init; }

        [JsonPropertyName("result_summary")]
        public string ResultSummary { get; set; } = string.Empty;
    }
}
