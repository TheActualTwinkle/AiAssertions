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

    private static IReadOnlyList<AiChatMessage> BuildWithTokenLimit(
        IReadOnlyList<AiChatMessage> messages,
        int maxTokens) =>
        CodebaseConversationCompactor.BuildRequestMessages(
            messages,
            customCompactor: null,
            compactionEnabled: false,
            recentToolCallTurns: 2,
            maxCompactedToolResultChars: 3000,
            maxRequestTokens: maxTokens,
            tokenEstimator: request => request.Sum(message => message.Content.Length));

    private static AiChatMessage Message(string role, string content) =>
        new()
        {
            Role = role,
            Content = content
        };
}
