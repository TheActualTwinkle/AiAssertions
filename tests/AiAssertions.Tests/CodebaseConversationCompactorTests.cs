using AiAssertions.Core.Agent;
using AiAssertions.Core.Abstractions;
using AiAssertions.Core.Models;
using FluentAssertions;
using Xunit;

namespace AiAssertions.Tests;

public sealed class CodebaseConversationCompactorTests
{
    [Fact]
    public async Task BuildRequestMessagesAsync_WhenHistoryIsBelowTrigger_ShouldKeepFullHistory()
    {
        var messages = new List<AiChatMessage>
        {
            Message("system", "system prompt"),
            Message("user", "requirement")
        };
        AddReadFileTurn(messages, "call-1", "README.md", 1, new string('x', 200));
        AddReadFileTurn(messages, "call-2", ".gitignore", 1, new string('y', 200));
        var client = new CheckpointClient("unused");

        var result = await BuildAdaptive(messages, client, new CodebaseConversationCheckpoint());

        result.Should().Equal(messages);
        client.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildRequestMessagesAsync_WhenHistoryCrossesTrigger_ShouldCreateDurableCheckpoint()
    {
        var messages = new List<AiChatMessage>
        {
            Message("system", "system prompt"),
            Message("user", "README commands and paths must be valid")
        };
        AddReadFileTurn(messages, "call-1", "README.md", 1, new string('a', 9_000));
        AddReadFileTurn(messages, "call-2", "README.md", 121, new string('b', 9_000));
        AddReadFileTurn(messages, "call-3", ".gitignore", 1, new string('c', 1_000));
        var client = new CheckpointClient(
            "Findings\n- README.md lines 1-240 were inspected.\nCoverage\n- Continue with remaining checks.\nUnresolved\n- Verify paths.");
        var checkpoint = new CodebaseConversationCheckpoint();

        var result = await BuildAdaptive(messages, client, checkpoint);

        client.Requests.Should().ContainSingle();
        checkpoint.Revision.Should().Be(1);
        var state = result.Single(message => message.Role == "user" && message.Content.Contains("checkpoint revision", StringComparison.Ordinal));
        state.Content.Should().Contain("README.md lines 1-240 were inspected");
        state.Content.Should().Contain("read README.md lines 1-120 of 500");
        state.Content.Should().Contain("read README.md lines 121-240 of 500");
        state.Content.Should().Contain("Do not repeat an exact completed call");
        result.Should().NotContain(message => message.ToolCallId == "call-1" || message.ToolCallId == "call-2");
        result.Should().Contain(message => message.ToolCallId == "call-3" && message.Role == "tool");

        checkpoint.PruneCompactedPrefix(messages);
        messages.Should().HaveCount(4);
        messages.Should().Contain(message => message.ToolCallId == "call-3");

        var nextRequest = await BuildAdaptive(messages, client, checkpoint);
        client.Requests.Should().ContainSingle("an unchanged history must not be summarized twice");
        nextRequest.Should().Contain(message => message.Content.Contains("checkpoint revision 1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BuildRequestMessagesAsync_WhenSingleProtectedTurnExceedsTrigger_ShouldCheckpointIt()
    {
        var messages = new List<AiChatMessage>
        {
            Message("system", "system prompt"),
            Message("user", "requirement")
        };
        AddReadFileTurn(messages, "call-1", "README.md", 1, new string('a', 18_000));
        var client = new CheckpointClient("Findings\n- README.md lines 1-120 inspected.\nCoverage\n- One page.\nUnresolved\n- Continue.");

        var result = await BuildAdaptive(messages, client, new CodebaseConversationCheckpoint());

        client.Requests.Should().ContainSingle();
        result.Should().Contain(message => message.Content.Contains("checkpoint revision 1", StringComparison.Ordinal));
        result.Should().NotContain(message => message.ToolCallId == "call-1");
    }

    [Fact]
    public async Task BuildRequestMessagesAsync_WhenCompletedCallWasRepeated_ShouldDeduplicateCoverage()
    {
        var messages = new List<AiChatMessage>
        {
            Message("system", "system prompt"),
            Message("user", "requirement")
        };
        AddReadFileTurn(messages, "call-1", "README.md", 1, new string('a', 9_000));
        AddReadFileTurn(messages, "call-2", "README.md", 1, new string('a', 9_000));
        AddReadFileTurn(messages, "call-3", ".gitignore", 1, new string('c', 1_000));
        var client = new CheckpointClient("Findings\n- README was inspected.\nCoverage\n- Complete.\nUnresolved\n- None.");

        var result = await BuildAdaptive(messages, client, new CodebaseConversationCheckpoint());

        var state = result.Single(message => message.Role == "user" && message.Content.Contains("checkpoint revision", StringComparison.Ordinal));
        state.Content.Split("read_file {").Should().HaveCount(2);
        state.Content.Should().Contain("repeated 2 times");
    }

    [Fact]
    public async Task BuildRequestMessagesAsync_WhenTextCompactionIsUnsupported_ShouldUseBoundedFallback()
    {
        var messages = new List<AiChatMessage>
        {
            Message("system", "system prompt"),
            Message("user", "requirement")
        };
        AddReadFileTurn(messages, "call-1", "README.md", 1, new string('a', 9_000));
        AddReadFileTurn(messages, "call-2", "README.md", 121, new string('b', 9_000));
        AddReadFileTurn(messages, "call-3", ".gitignore", 1, new string('c', 1_000));

        var result = await BuildAdaptive(messages, new UnsupportedCheckpointClient(), new CodebaseConversationCheckpoint());

        var state = result.Single(message => message.Role == "user" && message.Content.Contains("checkpoint revision", StringComparison.Ordinal));
        state.Content.Should().Contain("Deterministic fallback summary");
        state.Content.Should().Contain("read README.md lines 1-120 of 500");
    }

    [Fact]
    public async Task BuildRequestMessagesAsync_WhenCheckpointIsCreated_ShouldRespectConfiguredTokenLimit()
    {
        var messages = new List<AiChatMessage>
        {
            Message("system", "system prompt"),
            Message("user", "requirement")
        };
        AddReadFileTurn(messages, "call-1", "README.md", 1, new string('a', 18_000));
        var client = new CheckpointClient("Findings\n- README inspected.\nCoverage\n- One page.\nUnresolved\n- None.");
        const int maxTokens = 3_000;

        int EstimateRequest(IReadOnlyList<AiChatMessage> request) =>
            request.Sum(message => message.Role.Length + message.Content.Length);

        await BuildAdaptive(messages, client, new CodebaseConversationCheckpoint(), maxTokens, EstimateRequest);

        client.Requests.Should().ContainSingle();
        EstimateRequest(client.Requests[0].Messages).Should().BeLessThanOrEqualTo(maxTokens);
    }

    [Fact]
    public async Task BuildRequestMessagesAsync_WhenCheckpointPromptCannotFitTokenLimit_ShouldUseFallbackWithoutModelRequest()
    {
        var messages = new List<AiChatMessage>
        {
            Message("system", "system prompt"),
            Message("user", "requirement")
        };
        AddReadFileTurn(messages, "call-1", "README.md", 1, new string('a', 18_000));
        var client = new CheckpointClient("must not be requested");

        await BuildAdaptive(
            messages,
            client,
            new CodebaseConversationCheckpoint(),
            maxRequestTokens: 1,
            tokenEstimator: request => request.Sum(message => message.Role.Length + message.Content.Length));

        client.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildRequestMessagesAsync_WhenCompactedToolCallFailed_ShouldNotMarkItCompleted()
    {
        var messages = new List<AiChatMessage>
        {
            Message("system", "system prompt"),
            Message("user", "requirement"),
            new()
            {
                Role = "assistant",
                Content = string.Empty,
                ToolCalls =
                [
                    new AiToolCall
                    {
                        Id = "failed-call",
                        Name = "read_file",
                        ArgumentsJson = """{"path":"failed.txt"}"""
                    }
                ]
            },
            new()
            {
                Role = "tool",
                Name = "read_file",
                ToolCallId = "failed-call",
                Content = """{"error":"Transient failure."}"""
            }
        };
        AddReadFileTurn(messages, "call-2", "README.md", 1, new string('a', 18_000));
        AddReadFileTurn(messages, "call-3", ".gitignore", 1, new string('b', 1_000));
        var result = await BuildAdaptive(messages, new UnsupportedCheckpointClient(), new CodebaseConversationCheckpoint());

        var state = result.Single(message => message.Content.Contains("checkpoint revision", StringComparison.Ordinal));
        state.Content.Should().Contain("README.md");
        state.Content.Should().NotContain("failed.txt");
        state.Content.Should().NotContain("ERROR: Transient failure");
    }

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
    public void BuildRequestMessages_WhenCheckpointAndRecentTurnExceedTokenLimit_ShouldPreserveBothByShrinkingCheckpoint()
    {
        var checkpoint = Message(
            "user",
            "Compacted assertion checkpoint revision 1:\n" + new string('c', 1_000));
        var recent = Message("assistant", "latest evidence");
        var messages = new[]
        {
            Message("system", "s"),
            Message("user", "u"),
            checkpoint,
            recent
        };

        var result = CodebaseConversationCompactor.BuildRequestMessages(
            messages,
            compactionEnabled: false,
            recentToolCallTurns: 1,
            maxCompactedToolResultChars: 100,
            maxCompactedStateChars: 500,
            maxRequestTokens: 120,
            tokenEstimator: request => request.Sum(message => message.Content.Length));

        result.Should().Contain(recent);
        var fittedCheckpoint = result.Single(message => message.Content.StartsWith("Compacted assertion checkpoint", StringComparison.Ordinal));
        fittedCheckpoint.Content.Length.Should().BeLessThan(checkpoint.Content.Length);
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
            compactionEnabled: false,
            recentToolCallTurns: 2,
            maxCompactedToolResultChars: 3000,
            maxCompactedStateChars: 16_000,
            maxRequestTokens: maxTokens,
            tokenEstimator: request => request.Sum(message => message.Content.Length));

    private static Task<IReadOnlyList<AiChatMessage>> BuildAdaptive(
        IReadOnlyList<AiChatMessage> messages,
        IToolCallingClient client,
        CodebaseConversationCheckpoint checkpoint,
        int? maxRequestTokens = null,
        Func<IReadOnlyList<AiChatMessage>, int>? tokenEstimator = null) =>
        CodebaseConversationCompactor.BuildRequestMessagesAsync(
            messages,
            client,
            checkpoint,
            compactionEnabled: true,
            recentToolCallTurns: 1,
            maxCompactedToolResultChars: 3_000,
            maxCompactedStateChars: 4_000,
            maxRequestTokens,
            tokenEstimator,
            CancellationToken.None);

    private static void AddReadFileTurn(
        ICollection<AiChatMessage> messages,
        string callId,
        string path,
        int startLine,
        string content)
    {
        messages.Add(new AiChatMessage
        {
            Role = "assistant",
            Content = string.Empty,
            ToolCalls =
            [
                new AiToolCall
                {
                    Id = callId,
                    Name = "read_file",
                    ArgumentsJson = $$"""{"path":"{{path}}","start_line":{{startLine}}}"""
                }
            ]
        });
        messages.Add(new AiChatMessage
        {
            Role = "tool",
            Name = "read_file",
            ToolCallId = callId,
            Content = $$"""{"file":"{{path}}","start_line":{{startLine}},"end_line":{{startLine + 119}},"line_count":120,"total_lines":500,"has_more":true,"next_start_line":{{startLine + 120}},"content_truncated":false,"content":"{{content}}"}"""
        });
    }

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
