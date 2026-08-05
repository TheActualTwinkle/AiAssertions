using System.Diagnostics;
using System.Text.Json;
using AiAssertions.Core.Abstractions;
using AiAssertions.Core.Agent;
using AiAssertions.Core.Models;
using AiAssertions.Core.Tools.Abstractions;
using FluentAssertions;
using Xunit;

namespace AiAssertions.Tests;

public sealed class CodebaseAssertionEngineTests
{
    [Fact]
    public async Task EvaluateAsync_WhenToolCallIsRepeatedWithReorderedArguments_ShouldUseCachedResult()
    {
        using var directory = new TemporaryDirectory();
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
        using var directory = new TemporaryDirectory();
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
        using var directory = new TemporaryDirectory();
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
        using var directory = new TemporaryDirectory();
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
        using var directory = new TemporaryDirectory();
        var client = new RecordingClient(VerdictResponse());
        var engine = new CodebaseAssertionEngine(
            client,
            options: new CodebaseAssertionOptions { WorkingDirectory = directory.Path });

        await engine.EvaluateAsync("requirement");

        var prompt = client.Requests.Single().Messages[0].Content;
        prompt.Should().Contain("Analyze all pre-included evidence before calling tools.");
        prompt.Should().Contain("For universal requirements");
        prompt.Should().Contain("For existential, aggregate, threshold, or completeness requirements");
        prompt.Should().Contain("continue with next_offset while has_more is true");
        prompt.Should().Contain("no more than 4");
    }

    private static AiToolResponse ToolResponse(string id, string argumentsJson) =>
        new()
        {
            Content = null,
            ToolCalls =
            [
                new AiToolCall
                {
                    Id = id,
                    Name = "counting",
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

    private sealed class CountingTool : IAiTool
    {
        public string Name => "counting";

        public string Description => "Counts executions.";

        public string ParametersJsonSchema => """{"type":"object"}""";

        internal int Executions { get; private set; }

        public ValueTask<string> ExecuteAsync(
            string argumentsJson,
            ToolExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            Executions++;
            return ValueTask.FromResult("""{"value":1}""");
        }
    }

    private sealed class HangingTool : IAiTool
    {
        public string Name => "counting";

        public string Description => "Never completes.";

        public string ParametersJsonSchema => """{"type":"object"}""";

        public ValueTask<string> ExecuteAsync(
            string argumentsJson,
            ToolExecutionContext context,
            CancellationToken cancellationToken = default) =>
            new(new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously).Task);
    }

    private sealed class FailingOnceTool : IAiTool
    {
        public string Name => "counting";

        public string Description => "Fails once.";

        public string ParametersJsonSchema => """{"type":"object"}""";

        internal int Executions { get; private set; }

        public ValueTask<string> ExecuteAsync(
            string argumentsJson,
            ToolExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            Executions++;
            return Executions == 1
                ? throw new InvalidOperationException("Transient failure.")
                : ValueTask.FromResult("""{"value":1}""");
        }
    }

    private sealed class RecordingClient(params AiToolResponse[] responses) : IToolCallingClient
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

    private sealed class HangingClient : IToolCallingClient
    {
        public Task<AiToolResponse> GetToolResponseAsync(
            AiToolRequest request,
            CancellationToken cancellationToken = default) =>
            new TaskCompletionSource<AiToolResponse>(TaskCreationOptions.RunContinuationsAsynchronously).Task;

        public Task<AiTextResponse> GetResponseAsync(
            AiTextRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        internal TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"AiAssertions.Tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose() =>
            Directory.Delete(Path, recursive: true);
    }
}
