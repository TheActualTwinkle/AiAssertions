using AiAssertions.Core.Agent;
using AiAssertions.Core.Models;
using FluentAssertions;
using Xunit;

namespace AiAssertions.Tests;

public sealed class CodebaseConversationCompactorTests
{
    [Fact]
    public void BuildRequestMessages_WhenTokenLimitIsExceeded_ShouldPreserveSystemAndInitialUserMessages()
    {
        var system = Message("system", "system prompt");
        var initialUser = Message("user", "requirement");
        var messages = new[]
        {
            system,
            initialUser,
            Message("assistant", "older"),
            Message("assistant", "newer")
        };

        var result = BuildWithTokenLimit(messages, maxTokens: 1);

        result.Should().Equal(system, initialUser);
    }

    [Fact]
    public void BuildRequestMessages_WhenToolCallGroupExceedsTokenLimit_ShouldOmitToolCallAndResultsTogether()
    {
        var toolCall = new AiChatMessage
        {
            Role = "assistant",
            Content = string.Empty,
            ToolCalls =
            [
                new AiToolCall
                {
                    Id = "call-1",
                    Name = "read_file",
                    ArgumentsJson = "{}"
                }
            ]
        };
        var toolResult = new AiChatMessage
        {
            Role = "tool",
            Content = "large tool result",
            ToolCallId = "call-1"
        };
        var messages = new[]
        {
            Message("system", "s"),
            Message("user", "u"),
            toolCall,
            toolResult,
            Message("assistant", "latest")
        };

        var result = BuildWithTokenLimit(messages, maxTokens: 8);

        result.Should().NotContain(toolCall);
        result.Should().NotContain(toolResult);
        result.Should().Contain(messages[^1]);
    }

    [Fact]
    public void BuildRequestMessages_WhenNewestRemainingGroupExceedsTokenLimit_ShouldNotKeepOlderGroups()
    {
        var oldSmallMessage = Message("assistant", "old");
        var oversizedNewMessage = Message("assistant", "this-new-message-is-too-large");
        var messages = new[]
        {
            Message("system", "s"),
            Message("user", "u"),
            oldSmallMessage,
            oversizedNewMessage
        };

        var result = BuildWithTokenLimit(messages, maxTokens: 10);

        result.Should().NotContain(oversizedNewMessage);
        result.Should().NotContain(oldSmallMessage);
    }

    [Fact]
    public void BuildRequestMessages_WhenRecentToolGroupIsLarge_ShouldShrinkResultsAndPreserveProtocolGroup()
    {
        var toolCalls = Enumerable.Range(1, 4)
            .Select(index => new AiToolCall
            {
                Id = $"call-{index}",
                Name = "read_file",
                ArgumentsJson = $$"""{"path":"file-{{index}}.txt"}"""
            })
            .ToArray();
        var messages = new List<AiChatMessage>
        {
            Message("system", "s"),
            Message("user", "u"),
            new()
            {
                Role = "assistant",
                Content = string.Empty,
                ToolCalls = toolCalls
            }
        };

        messages.AddRange(toolCalls.Select(call => new AiChatMessage
        {
            Role = "tool",
            Content = new string('x', 2_000),
            ToolCallId = call.Id
        }));

        var result = CodebaseConversationCompactor.BuildRequestMessages(
            messages,
            customCompactor: null,
            compactionEnabled: true,
            recentToolCallTurns: 1,
            maxCompactedToolResultChars: 1_000,
            maxCompactedStateChars: 600,
            maxRequestTokens: 300,
            tokenEstimator: request => request.Sum(message => message.Content.Length) / 4);

        result.Any(message => message.Role == "assistant" && message.ToolCalls?.Count == 4).Should().BeTrue();
        result.Where(message => message.Role == "tool").Should().HaveCount(4);
        result.Where(message => message.Role == "tool").Should().OnlyContain(message => message.Content.Length < 2_000);
        result.Where(message => message.Role == "tool").Should().OnlyContain(message => IsValidJson(message.Content));
    }

    [Fact]
    public void BuildRequestMessages_WithoutTokenLimit_ShouldKeepLatestToolEvidenceIntact()
    {
        var largeResult = $$"""{"content":"{{new string('x', 5_000)}}"}""";
        var messages = new[]
        {
            Message("system", "s"),
            Message("user", "u"),
            new AiChatMessage
            {
                Role = "assistant",
                Content = string.Empty,
                ToolCalls =
                [
                    new AiToolCall
                    {
                        Id = "call-1",
                        Name = "read_file",
                        ArgumentsJson = "{}"
                    }
                ]
            },
            new AiChatMessage
            {
                Role = "tool",
                Content = largeResult,
                ToolCallId = "call-1"
            }
        };

        var result = CodebaseConversationCompactor.BuildRequestMessages(
            messages,
            customCompactor: null,
            compactionEnabled: true,
            recentToolCallTurns: 1,
            maxCompactedToolResultChars: 100,
            maxCompactedStateChars: 100,
            maxRequestTokens: null,
            tokenEstimator: null);

        result.Single(message => message.Role == "tool").Content.Should().Be(largeResult);
    }

    [Fact]
    public void BuildRequestMessages_WhenUnicodeResultMustBeShrunk_ShouldTerminateAndReturnTruncationMetadata()
    {
        var prefix = string.Concat("{\"value\":\"", new string('a', 30));
        var largeResult = string.Concat(prefix, "😀", new string('b', 200), "\"}");
        var messages = ToolTurn(largeResult);

        var result = CodebaseConversationCompactor.BuildRequestMessages(
            messages,
            customCompactor: null,
            compactionEnabled: false,
            recentToolCallTurns: 1,
            maxCompactedToolResultChars: 100,
            maxCompactedStateChars: 500,
            maxRequestTokens: 120,
            tokenEstimator: request => request.Sum(message => message.Content.Length));

        var content = result.Single(message => message.Role == "tool").Content;
        using var json = System.Text.Json.JsonDocument.Parse(content);
        json.RootElement.GetProperty("truncated").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void BuildRequestMessages_WhenOldHistoryCausesOverflow_ShouldDropItBeforeShrinkingNewestEvidence()
    {
        var newestResult = $$"""{"content":"{{new string('n', 400)}}"}""";
        var messages = new List<AiChatMessage>
        {
            Message("system", "s"),
            Message("user", "u"),
            Message("assistant", new string('o', 2_000))
        };
        messages.AddRange(ToolTurn(newestResult).Skip(2));

        var result = CodebaseConversationCompactor.BuildRequestMessages(
            messages,
            customCompactor: null,
            compactionEnabled: false,
            recentToolCallTurns: 1,
            maxCompactedToolResultChars: 100,
            maxCompactedStateChars: 500,
            maxRequestTokens: 500,
            tokenEstimator: request => request.Sum(message => message.Content.Length));

        result.Should().NotContain(message => message.Content.Length == 2_000);
        result.Single(message => message.Role == "tool").Content.Should().Be(newestResult);
    }

    [Fact]
    public void BuildRequestMessages_WhenOldStateIsLarge_ShouldBoundCompactedStateAndRecordOmissions()
    {
        var messages = new List<AiChatMessage>
        {
            Message("system", "s"),
            Message("user", "u")
        };

        for (var index = 1; index <= 5; index++)
        {
            messages.Add(new AiChatMessage
            {
                Role = "assistant",
                Content = string.Empty,
                ToolCalls =
                [
                    new AiToolCall
                    {
                        Id = $"call-{index}",
                        Name = "read_file",
                        ArgumentsJson = $$"""{"path":"file-{{index}}.txt"}"""
                    }
                ]
            });
            messages.Add(new AiChatMessage
            {
                Role = "tool",
                Content = new string('x', 2_000),
                ToolCallId = $"call-{index}"
            });
        }

        var result = CodebaseConversationCompactor.BuildRequestMessages(
            messages,
            customCompactor: null,
            compactionEnabled: true,
            recentToolCallTurns: 1,
            maxCompactedToolResultChars: 500,
            maxCompactedStateChars: 500,
            maxRequestTokens: null,
            tokenEstimator: null);

        var compactedState = result.Single(message => message.Role == "user" && message.Content.Contains("Compacted assertion state", StringComparison.Ordinal));
        compactedState.Content.Length.Should().BeLessThan(800);
        compactedState.Content.Should().Contain("omitted_tool_calls");
    }

    private static IReadOnlyList<AiChatMessage> BuildWithTokenLimit(
        IReadOnlyList<AiChatMessage> messages,
        int maxTokens) =>
        CodebaseConversationCompactor.BuildRequestMessages(
            messages,
            customCompactor: null,
            compactionEnabled: false,
            recentToolCallTurns: 2,
            maxCompactedToolResultChars: 3000,
            maxCompactedStateChars: 12_000,
            maxRequestTokens: maxTokens,
            tokenEstimator: request => request.Sum(message => message.Content.Length));

    private static AiChatMessage Message(string role, string content) =>
        new()
        {
            Role = role,
            Content = content
        };

    private static IReadOnlyList<AiChatMessage> ToolTurn(string content) =>
    [
        Message("system", "s"),
        Message("user", "u"),
        new AiChatMessage
        {
            Role = "assistant",
            Content = string.Empty,
            ToolCalls =
            [
                new AiToolCall
                {
                    Id = "call-1",
                    Name = "read_file",
                    ArgumentsJson = "{}"
                }
            ]
        },
        new AiChatMessage
        {
            Role = "tool",
            Content = content,
            ToolCallId = "call-1"
        }
    ];

    private static bool IsValidJson(string content)
    {
        try
        {
            using var _ = System.Text.Json.JsonDocument.Parse(content);
            return true;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
