using System.Text.Json;
using AiAssertions.Core.Abstractions;
using AiAssertions.Core.Models;
using FluentAssertions;
using Xunit;

namespace AiAssertions.Tests;

public sealed class CodebaseAssertionConfigurationTests
{
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
    public async Task CodebaseAssertion_WhenDefaultHistoryIsSmall_ShouldNotCompactPrematurely()
    {
        var client = new RecordingToolCallingClient(
            ToolCallResponse("call-1"),
            ToolCallResponse("call-2"),
            ToolCallResponse("call-3"),
            VerdictResponse());

        await CreateAssertion(client).That("requirement");

        var finalMessages = client.Requests[^1].Messages;
        finalMessages.Should().HaveCount(8);

        finalMessages.Select(message => message.Role).Should()
            .Equal("system", "user", "assistant", "tool", "assistant", "tool", "assistant", "tool");

        finalMessages.SelectMany(message => message.ToolCalls ?? []).Select(call => call.Id)
            .Should().Equal("call-1", "call-2", "call-3");
    }

    [Fact]
    public async Task CodebaseAssertion_WhenConversationCompactionIsConfiguredAfterDisabling_ShouldExecute()
    {
        var client = new RecordingToolCallingClient(VerdictResponse());

        await CreateAssertion(client)
            .WithoutConversationCompaction()
            .WithConversationCompaction(new ConversationCompactionOptions
            {
                RecentToolCallTurns = 1,
                MaxCheckpointChars = 8192
            })
            .That("requirement");

        client.Requests.Should().ContainSingle();
    }

    [Fact]
    public void CodebaseAssertion_WhenConversationCompactionOptionsAreNull_ShouldThrow()
    {
        var client = new RecordingToolCallingClient(VerdictResponse());

        var act = () => CreateAssertion(client).WithConversationCompaction(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Theory]
    [InlineData(0, 1, nameof(ConversationCompactionOptions.RecentToolCallTurns))]
    [InlineData(1, 0, nameof(ConversationCompactionOptions.MaxCheckpointChars))]
    public void CodebaseAssertion_WhenConversationCompactionOptionIsNotPositive_ShouldThrow(
        int recentToolCallTurns,
        int maxCheckpointChars,
        string parameterName)
    {
        var client = new RecordingToolCallingClient(VerdictResponse());
        var options = new ConversationCompactionOptions
        {
            RecentToolCallTurns = recentToolCallTurns,
            MaxCheckpointChars = maxCheckpointChars
        };

        var act = () => CreateAssertion(client).WithConversationCompaction(options);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(parameterName);
    }

    [Fact]
    public async Task CodebaseAssertion_WhenTokenLimitAndEstimatorAreConfiguredTogether_ShouldUseEstimator()
    {
        var client = new RecordingToolCallingClient(VerdictResponse());
        var estimatorCalls = 0;

        await CreateAssertion(client)
            .WithApproximateTokenLimit(4096, messages =>
            {
                estimatorCalls++;
                return messages.Sum(message => message.Content.Length);
            })
            .That("requirement");

        estimatorCalls.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CodebaseAssertion_WhenOnlyTokenLimitIsChanged_ShouldPreserveGlobalEstimator()
    {
        var client = new RecordingToolCallingClient(VerdictResponse());
        var estimatorCalls = 0;
        var defaults = new AiAssertDefaults
        {
            RequestTokenEstimator = messages =>
            {
                estimatorCalls++;
                return messages.Count;
            }
        };

        await CreateAssertion(client, defaults)
            .WithApproximateTokenLimit(4096)
            .That("requirement");

        estimatorCalls.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CodebaseAssertion_WhenEstimatorIsExplicitlyNull_ShouldUseBuiltInEstimator()
    {
        var client = new RecordingToolCallingClient(VerdictResponse());
        var estimatorCalls = 0;
        var defaults = new AiAssertDefaults
        {
            RequestTokenEstimator = messages =>
            {
                estimatorCalls++;
                return messages.Count;
            }
        };

        await CreateAssertion(client, defaults)
            .WithApproximateTokenLimit(4096, null)
            .That("requirement");

        estimatorCalls.Should().Be(0);
    }

    [Fact]
    public async Task CodebaseAssertion_WhenGlobalSystemPromptsAreConfigured_ShouldUseThem()
    {
        var client = new RecordingToolCallingClient(VerdictResponse());
        var defaults = new AiAssertDefaults
        {
            SystemPrompt = "global system prompt",
            AdditionalSystemPrompt = "global additional instruction"
        };

        await CreateAssertion(client, defaults).That("requirement");

        var separator = Environment.NewLine + Environment.NewLine;
        client.Requests[0].Messages[0].Content.Should()
            .Be($"global system prompt{separator}global additional instruction");
    }

    [Fact]
    public async Task CodebaseAssertion_WhenGlobalTokenDefaultsAreConfigured_ShouldApplyThem()
    {
        var client = new RecordingToolCallingClient(VerdictResponse());
        var estimatorCalls = 0;

        var defaults = new AiAssertDefaults
        {
            MaxRequestTokens = 4096,
            RequestTokenEstimator = messages =>
            {
                estimatorCalls++;
                return messages.Sum(message => message.Content.Length);
            }
        };

        await CreateAssertion(client, defaults).That("requirement");

        estimatorCalls.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task WithExecutionTrace_ShouldExposePublicExecutionTraceOnResult()
    {
        var client = new RecordingToolCallingClient(ToolCallResponse("call-1"), VerdictResponse());

        var result = await CreateAssertion(client)
            .WithExecutionTrace()
            .That("requirement");

        result.ExecutionTrace.Should().NotBeNull();
        result.ExecutionTrace!.Entries.Should().HaveCount(4);

        var firstExchange = result.ExecutionTrace.Entries[0]
            .Should().BeOfType<CodebaseAssertionModelExchangeTraceEntry>().Subject;
        firstExchange.Request.Messages.Should().Contain(message => message.Content.Contains("requirement"));
        firstExchange.Response!.ToolCalls.Should().ContainSingle(call => call.Id == "call-1");
        firstExchange.Error.Should().BeNull();

        var toolExecution = result.ExecutionTrace.Entries[1]
            .Should().BeOfType<CodebaseAssertionToolExecutionTraceEntry>().Subject;
        toolExecution.ToolCallId.Should().Be("call-1");
        toolExecution.ToolName.Should().Be("list_projects");
        toolExecution.Arguments.ValueKind.Should().Be(JsonValueKind.Object);
        toolExecution.Result.Should().NotBeNull();
        toolExecution.CacheHit.Should().BeFalse();
        toolExecution.Error.Should().BeNull();

        result.ExecutionTrace.Entries[2].Should().BeOfType<CodebaseAssertionModelExchangeTraceEntry>();
        var completed = result.ExecutionTrace.Entries[3]
            .Should().BeOfType<CodebaseAssertionRunCompletedTraceEntry>().Subject;
        completed.Passed.Should().BeTrue();
        completed.IsConclusive.Should().BeTrue();

        var json = result.ExecutionTrace.ToJson();
        json.Should().Contain("\"kind\": \"modelExchange\"");
        json.Should().Contain("\"kind\": \"toolExecution\"");
        json.Should().Contain("requirement");
        json.Should().NotContain("\"payload\"");
        json.Should().NotContain("payloadJson");
    }

    [Fact]
    public async Task CodebaseAssertion_WhenGlobalExecutionTraceIsEnabled_ShouldCaptureTrace()
    {
        var client = new RecordingToolCallingClient(VerdictResponse());
        var defaults = new AiAssertDefaults { ExecutionTraceEnabled = true };

        var result = await CreateAssertion(client, defaults).That("requirement");

        result.ExecutionTrace.Should().NotBeNull();
    }

    private static CodebaseAssertion CreateAssertion(
        IToolCallingClient client,
        AiAssertDefaults? defaults = null) =>
        new(client, defaults ?? new AiAssertDefaults());

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
}
