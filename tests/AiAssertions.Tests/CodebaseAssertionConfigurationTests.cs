using AiAssertions.Core.Abstractions;
using AiAssertions.Core.Models;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace AiAssertions.Tests;

public sealed class CodebaseAssertionConfigurationTests
{
    private const string DefaultSystemPromptStart = "You are AiAssert, a strict codebase assertion agent.";

    private const string Verdict = """
                                   ```json
                                   {"passed":true,"confidence":1.0,"is_conclusive":true,"reason":"ok","evidence":[],"missing_evidence":[]}
                                   ```
                                   """;

    [Fact]
    public async Task CodebaseAssertion_WhenConversationCompactionIsDisabled_ShouldSendFullConversationHistory()
    {
        var client = new RecordingToolCallingClient(
            ToolCallResponse("call-1"),
            ToolCallResponse("call-2"),
            ToolCallResponse("call-3"),
            VerdictResponse());

        await CreateAssertion(client)
            .WithoutConversationCompaction()
            .That("requirement");

        client.Requests.Should().HaveCount(4);
        client.Requests[^1].Messages.Should().HaveCount(8);

        client.Requests[^1].Messages
            .Select(message => message.Role)
            .Should()
            .Equal("system", "user", "assistant", "tool", "assistant", "tool", "assistant", "tool");
    }

    [Fact]
    public async Task CodebaseAssertion_WhenConversationCompactionUsesDefaults_ShouldCompactOldToolCallsAndKeepRecentTurns()
    {
        var client = new RecordingToolCallingClient(
            ToolCallResponse("call-1"),
            ToolCallResponse("call-2"),
            ToolCallResponse("call-3"),
            VerdictResponse());

        await CreateAssertion(client).That("requirement");

        var finalMessages = client.Requests[^1].Messages;
        finalMessages.Should().HaveCount(7);

        finalMessages.Select(message => message.Role).Should()
            .Equal("system", "user", "user", "assistant", "tool", "assistant", "tool");

        var compactedState = ParseCompactedState(finalMessages[2].Content);
        var completedToolCalls = compactedState.RootElement.GetProperty("completed_tool_calls");

        completedToolCalls.GetArrayLength().Should().Be(1);

        var completedToolCall = completedToolCalls[0];
        completedToolCall.GetProperty("tool").GetString().Should().Be("list_projects");
        completedToolCall.GetProperty("arguments").ValueKind.Should().Be(JsonValueKind.Object);
        completedToolCall.GetProperty("arguments").EnumerateObject().Should().BeEmpty();

        var resultSummary = completedToolCall.GetProperty("result_summary").GetString();
        resultSummary.Should().NotBeNullOrWhiteSpace();

        using var result = JsonDocument.Parse(resultSummary!);
        result.RootElement.GetProperty("projects").EnumerateArray()
            .Select(project => project.GetString())
            .Should().Contain("src/AiAssertions.Core/AiAssertions.Core.csproj");

        finalMessages.SelectMany(message => message.ToolCalls ?? []).Select(call => call.Id)
            .Should().Equal("call-2", "call-3");
    }

    [Fact]
    public async Task CodebaseAssertion_WhenCustomCompactorIsConfigured_ShouldReceiveFullAccumulatedConversation()
    {
        var client = new RecordingToolCallingClient(
            ToolCallResponse("call-1"),
            VerdictResponse());

        var receivedHistories = new List<AiChatMessage[]>();

        await CreateAssertion(client)
            .WithConversationCompactor(messages =>
            {
                receivedHistories.Add(messages.ToArray());

                return messages;
            })
            .That("requirement");

        receivedHistories.Should().HaveCount(2);
        receivedHistories[0].Select(message => message.Role).Should().Equal("system", "user");
        receivedHistories[1].Select(message => message.Role).Should().Equal("system", "user", "assistant", "tool");
        receivedHistories[1][2].ToolCalls.Should().ContainSingle(call => call.Id == "call-1");
        client.Requests[1].Messages.Should().Equal(receivedHistories[1]);
    }

    [Fact]
    public async Task CodebaseAssertion_WhenCustomCompactorIsDisabled_ShouldStopCallingCustomCompactor()
    {
        var client = new RecordingToolCallingClient(VerdictResponse());
        var calls = 0;

        await CreateAssertion(client)
            .WithConversationCompactor(messages =>
            {
                calls++;

                return messages;
            })
            .WithoutConversationCompaction()
            .That("requirement");

        calls.Should().Be(0);
    }

    [Fact]
    public async Task CodebaseAssertion_WhenCustomCompactorIsConfiguredAfterDisablingCompaction_ShouldUseCustomCompactor()
    {
        var client = new RecordingToolCallingClient(VerdictResponse());
        var calls = 0;

        await CreateAssertion(client)
            .WithoutConversationCompaction()
            .WithConversationCompactor(messages =>
            {
                calls++;

                return messages;
            })
            .That("requirement");

        calls.Should().Be(1);
    }

    [Fact]
    public async Task CodebaseAssertion_WhenCustomCompactorAndTokenLimitAreConfigured_ShouldCompactBeforeApplyingTokenLimitAndRespectBudget()
    {
        var client = new RecordingToolCallingClient(VerdictResponse());
        var calls = 0;
        var oldLargeMessage = Message("assistant", "old-message-that-does-not-fit");
        var newestMessage = Message("assistant", "new");
        const int maxTokens = 103;

        await CreateAssertion(client)
            .WithConversationCompactor(messages =>
            {
                calls++;

                return [messages[0], messages[1], oldLargeMessage, newestMessage];
            })
            .WithApproximateTokenLimit(maxTokens)
            .WithTokenEstimator(EstimateTokens)
            .That("requirement");

        calls.Should().Be(1);
        client.Requests[0].Messages.Should().Contain(newestMessage);
        client.Requests[0].Messages.Should().NotContain(oldLargeMessage);
        EstimateTokens(client.Requests[0].Messages).Should().BeLessThanOrEqualTo(maxTokens);

        return;

        int EstimateTokens(IReadOnlyList<AiChatMessage> messages) =>
            100 + messages.Sum(message => ReferenceEquals(message, oldLargeMessage) ? 10_000 : 1);
    }

    [Fact]
    public async Task CodebaseAssertion_WhenSystemPromptIsConfigured_ShouldReplaceDefaultSystemPrompt()
    {
        var client = new RecordingToolCallingClient(VerdictResponse());

        await CreateAssertion(client)
            .WithSystemPrompt("replacement prompt")
            .That("requirement");

        client.Requests[0].Messages[0].Content.Should().Be("replacement prompt");
    }

    [Fact]
    public async Task CodebaseAssertion_WhenMultipleAdditionalSystemPromptsAreConfigured_ShouldAppendPromptsInCallOrder()
    {
        var client = new RecordingToolCallingClient(VerdictResponse());

        await CreateAssertion(client)
            .WithSystemPrompt("base")
            .WithAdditionalSystemPrompt("first")
            .WithAdditionalSystemPrompt("second")
            .That("requirement");

        var separator = Environment.NewLine + Environment.NewLine;
        client.Requests[0].Messages[0].Content.Should().Be($"base{separator}first{separator}second");
    }

    [Fact]
    public async Task CodebaseAssertion_WhenAdditionalSystemPromptIsConfigured_ShouldAppendItToDefaultSystemPrompt()
    {
        var client = new RecordingToolCallingClient(VerdictResponse());

        await CreateAssertion(client)
            .WithAdditionalSystemPrompt("additional instruction")
            .That("requirement");

        var systemPrompt = client.Requests[0].Messages[0].Content;
        systemPrompt.Should().StartWith(DefaultSystemPromptStart);
        systemPrompt.Should().EndWith($"{Environment.NewLine}{Environment.NewLine}additional instruction");
    }

    private static CodebaseAssertion CreateAssertion(IToolCallingClient client) =>
        new(client, new AiAssertDefaults());

    private static AiToolResponse ToolCallResponse(string id) =>
        new()
        {
            Content = null,
            ToolCalls =
            [
                new AiToolCall
                {
                    Id = id,
                    Name = "list_projects",
                    ArgumentsJson = "{}"
                }
            ]
        };

    private static AiToolResponse VerdictResponse() =>
        new()
        {
            Content = Verdict,
            ToolCalls = []
        };

    private static AiChatMessage Message(string role, string content) =>
        new()
        {
            Role = role,
            Content = content
        };

    private static JsonDocument ParseCompactedState(string content)
    {
        const string jsonFence = "```json";
        var jsonStart = content.IndexOf(jsonFence, StringComparison.Ordinal);
        jsonStart.Should().BeGreaterThanOrEqualTo(0);
        jsonStart += jsonFence.Length;

        var jsonEnd = content.IndexOf("```", jsonStart, StringComparison.Ordinal);
        jsonEnd.Should().BeGreaterThan(jsonStart);

        return JsonDocument.Parse(content[jsonStart..jsonEnd]);
    }

    private sealed class RecordingToolCallingClient(params AiToolResponse[] responses) : IToolCallingClient
    {
        private readonly Queue<AiToolResponse> _responses = new(responses);

        internal List<AiToolRequest> Requests { get; } = [];

        public Task<AiToolResponse> GetToolResponseAsync(
            AiToolRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);

            return Task.FromResult(_responses.Dequeue());
        }

        public Task<AiTextResponse> GetResponseAsync(
            AiTextRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
