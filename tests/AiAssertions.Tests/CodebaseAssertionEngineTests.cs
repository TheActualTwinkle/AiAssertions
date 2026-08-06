using System.Diagnostics;
using System.Text.Json;
using AiAssertions.Core.Agent;
using AiAssertions.Core.Assertions;
using AiAssertions.Core.Models;
using FluentAssertions;
using Xunit;

namespace AiAssertions.Tests;

public sealed class CodebaseAssertionEngineTests
{
    [Fact]
    public async Task EvaluateAsync_WhenToolCallIsRepeatedWithReorderedArguments_ShouldUseCachedResult()
    {
        using var directory = new EngineTestTemporaryDirectory();
        var tool = new CountingTool();
        var client = new RecordingClient(
            ToolResponse("call-1", """{"second":2,"first":1}"""),
            ToolResponse("call-2", """{"first":1,"second":2}"""),
            VerdictResponse());
        var engine = new CodebaseAssertionEngine(
            client,
            [tool],
            new CodebaseAssertionOptions { WorkingDirectory = directory.Path });

        var result = await engine.EvaluateAsync("requirement");

        result.Passed.Should().BeTrue();
        tool.Executions.Should().Be(1);
        var cachedMessage = client.Requests[^1].Messages.Single(message => message.ToolCallId == "call-2");
        using var cachedResult = JsonDocument.Parse(cachedMessage.Content);
        cachedResult.RootElement.GetProperty("cached").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_WhenToolFails_ShouldRetryInsteadOfCachingError()
    {
        using var directory = new EngineTestTemporaryDirectory();
        var tool = new FailingOnceTool();
        var client = new RecordingClient(
            ToolResponse("call-1", "{}"),
            ToolResponse("call-2", "{}"),
            VerdictResponse());
        var engine = new CodebaseAssertionEngine(
            client,
            [tool],
            new CodebaseAssertionOptions { WorkingDirectory = directory.Path });

        var result = await engine.EvaluateAsync("requirement");

        result.Passed.Should().BeTrue();
        tool.Executions.Should().Be(2);
        var retriedMessage = client.Requests[^1].Messages.Single(message => message.ToolCallId == "call-2");
        using var retriedResult = JsonDocument.Parse(retriedMessage.Content);
        retriedResult.RootElement.TryGetProperty("cached", out _).Should().BeFalse();
        retriedResult.RootElement.GetProperty("value").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task EvaluateAsync_WhenClientIgnoresCancellation_ShouldStillReturnAtTimeout()
    {
        using var directory = new EngineTestTemporaryDirectory();
        var engine = new CodebaseAssertionEngine(
            new HangingClient(),
            options: new CodebaseAssertionOptions
            {
                WorkingDirectory = directory.Path,
                Timeout = TimeSpan.FromMilliseconds(100)
            });
        var stopwatch = Stopwatch.StartNew();

        var result = await engine.EvaluateAsync("requirement");

        stopwatch.Stop();
        result.IsConclusive.Should().BeFalse();
        result.Reason.Should().Contain("Timed out");
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task EvaluateAsync_WhenToolIgnoresCancellation_ShouldStillReturnAtTimeout()
    {
        using var directory = new EngineTestTemporaryDirectory();
        var client = new RecordingClient(ToolResponse("call-1", "{}"));
        var engine = new CodebaseAssertionEngine(
            client,
            [new HangingTool()],
            new CodebaseAssertionOptions
            {
                WorkingDirectory = directory.Path,
                Timeout = TimeSpan.FromMilliseconds(100)
            });
        var stopwatch = Stopwatch.StartNew();

        var result = await engine.EvaluateAsync("requirement");

        stopwatch.Stop();
        result.IsConclusive.Should().BeFalse();
        result.Reason.Should().Contain("Timed out");
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task EvaluateAsync_ShouldTellModelToUseIncludedEvidenceAndStopAtFirstCounterexample()
    {
        using var directory = new EngineTestTemporaryDirectory();
        var client = new RecordingClient(VerdictResponse());
        var engine = new CodebaseAssertionEngine(
            client,
            options: new CodebaseAssertionOptions { WorkingDirectory = directory.Path });

        await engine.EvaluateAsync("requirement");

        var prompt = client.Requests.Single().Messages[0].Content;
        prompt.Should().Contain("Analyze all pre-included evidence before calling tools.");
        prompt.Should().Contain("For universal requirements");
        prompt.Should().Contain("For existential, aggregate, threshold, or completeness requirements");
        prompt.Should().Contain("continue with next_offset or next_start_line while has_more is true");
        prompt.Should().Contain("Do not repeat exact completed calls recorded in its coverage ledger");
        prompt.Should().Contain("\"confidence\" must be a JSON number from 0.0 to 1.0 inclusive");
        prompt.Should().Contain("no more than 4");
    }

    [Fact]
    public async Task EvaluateAsync_WhenExecutionTraceIsEnabled_ShouldCaptureModelAndToolExchanges()
    {
        using var directory = new EngineTestTemporaryDirectory();
        var client = new RecordingClient(
            ToolResponse("call-1", "{}"),
            VerdictResponse());
        var engine = new CodebaseAssertionEngine(
            client,
            [new CountingTool()],
            new CodebaseAssertionOptions
            {
                WorkingDirectory = directory.Path,
                ExecutionTraceEnabled = true
            });

        var result = await engine.EvaluateAsync("requirement");

        result.ExecutionTrace.Should().NotBeNull();
        result.ExecutionTrace!.Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        result.ExecutionTrace.CompletedAtUtc.Should().BeOnOrAfter(result.ExecutionTrace.StartedAtUtc);
        result.ExecutionTrace.Entries.Select(entry => entry.Kind).Should().ContainInOrder(
            AiAssertionExecutionTraceEntryKind.ModelExchange,
            AiAssertionExecutionTraceEntryKind.ToolExecution,
            AiAssertionExecutionTraceEntryKind.ModelExchange,
            AiAssertionExecutionTraceEntryKind.RunCompleted);

        var toolEntry = result.ExecutionTrace.Entries.Single(
            entry => entry.Kind == AiAssertionExecutionTraceEntryKind.ToolExecution);
        toolEntry.Name.Should().Be("counting");
        toolEntry.PayloadJson.Should().Contain("call-1");
        toolEntry.PayloadJson.Should().Contain("result");
        toolEntry.PayloadJson.Should().Contain("value");

        var modelEntries = result.ExecutionTrace.Entries
            .Where(entry => entry.Kind == AiAssertionExecutionTraceEntryKind.ModelExchange)
            .ToArray();
        modelEntries.Should().HaveCount(2);
        modelEntries[0].PayloadJson.Should().Contain("requirement");
        using var finalExchange = JsonDocument.Parse(modelEntries[1].PayloadJson);
        finalExchange.RootElement
            .GetProperty("response")
            .GetProperty("content")
            .GetString()
            .Should()
            .Contain("\"passed\":true");
    }

    [Fact]
    public async Task EvaluateAsync_WhenExecutionTraceIsDisabled_ShouldNotAllocateResultTrace()
    {
        using var directory = new EngineTestTemporaryDirectory();
        var engine = new CodebaseAssertionEngine(
            new RecordingClient(VerdictResponse()),
            options: new CodebaseAssertionOptions { WorkingDirectory = directory.Path });

        var result = await engine.EvaluateAsync("requirement");

        result.ExecutionTrace.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_WhenConversationIsCompacted_ShouldCaptureCheckpointExchangeAndCompaction()
    {
        using var directory = new EngineTestTemporaryDirectory();
        var client = new CheckpointRecordingToolCallingClient(
            "Findings\n- Large result inspected.\nCoverage\n- Complete.\nUnresolved\n- None.",
            ToolResponse("call-1", "{}", "large_result"),
            VerdictResponse());
        var engine = new CodebaseAssertionEngine(
            client,
            [new LargeResultTool()],
            new CodebaseAssertionOptions
            {
                WorkingDirectory = directory.Path,
                ExecutionTraceEnabled = true,
                RecentToolCallTurns = 1
            });

        var result = await engine.EvaluateAsync("requirement");

        result.ExecutionTrace.Should().NotBeNull();
        var checkpointExchange = result.ExecutionTrace!.Entries.Single(
            entry => entry.Kind == AiAssertionExecutionTraceEntryKind.ConversationCompactionModelExchange);
        checkpointExchange.PayloadJson.Should().Contain("Large result inspected");

        var compaction = result.ExecutionTrace.Entries.Single(
            entry => entry.Kind == AiAssertionExecutionTraceEntryKind.ConversationCompaction);
        compaction.Name.Should().Be("revision_1");
        compaction.PayloadJson.Should().Contain("semantic_summary");
        compaction.PayloadJson.Should().Contain("removed_message_count");
    }

    [Fact]
    public async Task EvaluateAsync_WhenExecutionTraceIsEnabledAndRunTimesOut_ShouldReturnCompletedTrace()
    {
        using var directory = new EngineTestTemporaryDirectory();
        var engine = new CodebaseAssertionEngine(
            new HangingClient(),
            options: new CodebaseAssertionOptions
            {
                WorkingDirectory = directory.Path,
                Timeout = TimeSpan.FromMilliseconds(100),
                ExecutionTraceEnabled = true
            });

        var result = await engine.EvaluateAsync("requirement");

        result.ExecutionTrace.Should().NotBeNull();
        result.ExecutionTrace!.Entries.Should().ContainSingle(
            entry => entry.Kind == AiAssertionExecutionTraceEntryKind.RunCompleted);
        result.ExecutionTrace.Entries[^1].PayloadJson.Should().Contain("Timed out");
    }

    private static AiToolResponse ToolResponse(
        string id,
        string argumentsJson,
        string name = "counting") =>
        new()
        {
            Content = null,
            ToolCalls =
            [
                new AiToolCall
                {
                    Id = id,
                    Name = name,
                    ArgumentsJson = argumentsJson
                }
            ]
        };

    private static AiToolResponse VerdictResponse() =>
        new()
        {
            Content = """{"passed":true,"confidence":1,"is_conclusive":true,"reason":"ok","evidence":[],"missing_evidence":[]}""",
            ToolCalls = []
        };

}
