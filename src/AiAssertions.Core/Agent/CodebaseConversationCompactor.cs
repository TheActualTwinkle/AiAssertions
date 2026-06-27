using System.Text;
using System.Text.Json;
using AiAssertions.Core.Assertions;
using AiAssertions.Core.Models;

namespace AiAssertions.Core.Agent;

internal static class CodebaseConversationCompactor
{
    internal static IReadOnlyList<AiChatMessage> BuildRequestMessages(
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
            if (message.Role == "assistant" && message.ToolCalls is { Count: > 0 })
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
