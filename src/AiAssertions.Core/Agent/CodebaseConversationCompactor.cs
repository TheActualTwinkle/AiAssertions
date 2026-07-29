using System.Text;
using System.Text.Json;
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
        int? maxRequestTokens,
        Func<IReadOnlyList<AiChatMessage>, int>? tokenEstimator)
    {
        var requestMessages = customCompactor is not null
            ? customCompactor(messages)
            : compactionEnabled
                ? BuildCompactedRequestMessages(messages, recentToolCallTurns, maxCompactedToolResultChars)
                : messages;

        return maxRequestTokens is > 0
            ? ApplyTokenLimit(requestMessages, maxRequestTokens.Value, tokenEstimator)
            : requestMessages;
    }

    private static IReadOnlyList<AiChatMessage> BuildCompactedRequestMessages(
        IReadOnlyList<AiChatMessage> messages,
        int recentToolCallTurns,
        int maxCompactedToolResultChars)
    {
        if (messages.Count <= 2)
            return messages;

        var recentStart = FindRecentStartIndex(messages, Math.Max(recentToolCallTurns, 1));
        if (recentStart <= 2)
            return messages;

        var compacted = BuildCompactedState(messages.Skip(2).Take(recentStart - 2), maxCompactedToolResultChars);
        if (string.IsNullOrWhiteSpace(compacted))
            return messages;

        var requestMessages = new List<AiChatMessage>(messages.Count - recentStart + 3)
        {
            messages[0],
            messages[1],
            new()
            {
                Role = "user",
                Content = compacted
            }
        };

        requestMessages.AddRange(messages.Skip(recentStart));

        return requestMessages;
    }

    private static IReadOnlyList<AiChatMessage> ApplyTokenLimit(
        IReadOnlyList<AiChatMessage> messages,
        int maxRequestTokens,
        Func<IReadOnlyList<AiChatMessage>, int>? tokenEstimator)
    {
        var estimateTokens = tokenEstimator ?? EstimateTokens;

        if (estimateTokens(messages) <= maxRequestTokens || messages.Count <= 2)
            return messages;

        var mandatoryMessages = messages.Take(2).ToArray();
        var mandatoryTokens = estimateTokens(mandatoryMessages);

        if (mandatoryTokens >= maxRequestTokens)
            return mandatoryMessages;

        var groups = BuildMessageGroups(messages.Skip(2)).ToArray();
        var selectedGroups = new Stack<IReadOnlyList<AiChatMessage>>();
        var omittedMessages = 0;

        for (var index = groups.Length - 1; index >= 0; index--)
        {
            var group = groups[index];
            var candidate = mandatoryMessages
                .Concat(group)
                .Concat(selectedGroups.SelectMany(selectedGroup => selectedGroup))
                .ToArray();

            if (estimateTokens(candidate) <= maxRequestTokens)
            {
                selectedGroups.Push(group);
            }
            else
            {
                omittedMessages += groups
                    .Take(index + 1)
                    .Sum(omittedGroup => omittedGroup.Count);
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

    private static string BuildCompactedState(IEnumerable<AiChatMessage> compactedMessages, int maxToolResultChars)
    {
        var entries = new List<object>();
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
            entries.Add(new
            {
                tool = message.Name,
                arguments = TryReadJson(toolCall?.ArgumentsJson),
                result_summary = SummarizeToolResult(message.Content, maxToolResultChars)
            });
        }

        if (entries.Count == 0)
            return string.Empty;

        var builder = new StringBuilder();
        builder.AppendLine("Compacted assertion state from earlier tool calls:");
        builder.AppendLine("```json");
        builder.AppendLine(JsonSerializer.Serialize(new { completed_tool_calls = entries }, AssertionJson.Options));
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

        return string.Concat(content.AsSpan(0, maxChars), "... [truncated]");
    }
}
