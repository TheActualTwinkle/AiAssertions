using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiAssertions.Core.Abstractions;
using AiAssertions.Core.Assertions;
using AiAssertions.Core.Models;
using AiAssertions.Core.Tools.Abstractions;
using AiAssertions.Core.Tools.Codebase;

namespace AiAssertions.Core.Agent;

internal sealed class CodebaseAssertionEngine
{
    private const string DefaultSystemPrompt = """
                                                You are AiAssert, a strict codebase assertion agent.
                                                Decide whether the requirement is satisfied by gathering evidence with tools.
                                                You cannot access the filesystem directly. Use only the provided tools.
                                                The user message includes execution_context.codebase_root. Use that root for tool calls.
                                                Do not guess when evidence can be gathered.
                                                If the conversation contains a compacted assertion checkpoint or state, treat it as previously gathered tool evidence.
                                                Do not repeat exact completed calls recorded in its coverage ledger. If a missing detail is genuinely required, prefer a narrower targeted search or read only the missing range.

                                                Analyze all pre-included evidence before calling tools.
                                                First identify the logical shape of the requirement.
                                                For universal requirements such as all, every, always, never, only, or must, one concrete counterexample is sufficient for a failed verdict. Return that verdict immediately when the counterexample is conclusive.
                                                For existential, aggregate, threshold, or completeness requirements, do not infer a failed verdict from one non-matching example. Gather enough evidence for the requirement's actual quantifier.
                                                Search and file-read responses are paginated. For exhaustive or universal passing verdicts, continue with next_offset or next_start_line while has_more is true. Never treat one page as the complete result set.
                                                Stop gathering evidence as soon as the verdict is logically conclusive, but not before.
                                                Never claim that runtime behavior was verified unless a tool actually executed it or concrete code evidence proves it. Distinguish static evidence from executed behavior.
                                                Do not call list_projects or search_files when the relevant files are already present in pre-included evidence.
                                                Keep discovery queries narrow and relevant to minimize noise and token usage. Discovery tools exclude paths matched by .gitignore by default, but read_file can read an explicitly named ignored file. 
                                                When relevant evidence or documentation provides an exact path, call read_file directly even if discovery did not return it. Use include_ignored=true only for a targeted search when ignored files are explicitly relevant and the exact path is unknown.

                                                Batch up to 4 independent tool calls when necessary.
                                                If you know several files, names, or searches that are all needed, request no more than 4 of those tool calls in the same assistant turn.
                                                Prefer one broad discovery turn followed by one batched evidence-reading turn over many single-tool turns.
                                                Do not wait for read_file("A.cs") before requesting read_file("B.cs") when both files are already known.

                                                When you ready to return a verdict, do not call any more tools and return the JSON in a single code block.
                                                Return the final verdict as strict JSON only:
                                                {"passed":true|false,"confidence":0.0-1.0,"is_conclusive":true|false,"reason":"...","evidence":[{"file":"...","start_line":1,"end_line":3,"description":"..."}],"missing_evidence":[{"description":"...","expected_location":"..."}]}
                                                If you cannot find enough relevant code or evidence, return "is_conclusive": false, "passed": false, and explain what is missing.
                                                Most important rule:
                                                Never return any other text outside the JSON code block. Do not include any additional commentary or explanations.
                                                "confidence" must be a JSON number from 0.0 to 1.0 inclusive, where 0.0 means no confidence and 1.0 means complete confidence in the verdict.
                                                "reason" must be a concise summary of the evidence and reasoning behind the verdict with max 150 characters.
                                                "evidence" must contain only concrete code evidence with exact file paths (relative to codebase root) and one-based line ranges.
                                                "missing_evidence" must describe relevant evidence that was expected or needed but not found.
                                                If any of this rules are violated, the verdict will be considered invalid and the assertion will fail.

                                                NEVER ESCAPE FILE PATHS. Use forward slashes (/) only as directory separators, even on Windows.
                                                e.g.: "file":"SampleCode/Security/PasswordRegistrationService.cs"

                                                THIS IS AN EXAMPLE OF A GOOD VERDICT:
                                                ```json
                                                {"passed":true,"confidence":0.93,"is_conclusive":true,"reason":"Password is hashed with salt before storage; no plain text stored or logged.","evidence":[{"file":"SampleCode/Security/PasswordRegistrationService.cs","start_line":12,"end_line":22,"description":"Password hash and salt are created before user registration."},{"file":"SampleCode/Security/RegisteredUser.cs","start_line":3,"end_line":8,"description":"Registered user stores only password hash and salt."}],"missing_evidence":[]}
                                                ```
                                                """;

    private readonly IToolCallingClient _client;
    private readonly CodebaseAssertionOptions _options;
    private readonly IReadOnlyDictionary<string, IAiTool> _toolsByName;
    private readonly IReadOnlyList<AiToolDefinition> _toolDefinitions;

    internal CodebaseAssertionEngine(
        IToolCallingClient client,
        IEnumerable<IAiTool>? tools = null,
        CodebaseAssertionOptions? options = null)
    {
        _client = client;
        IReadOnlyList<IAiTool> tools1 = (tools ?? DefaultCodebaseTools.Create()).ToArray();
        _toolsByName = tools1.ToDictionary(tool => tool.Name, StringComparer.Ordinal);
        _toolDefinitions = tools1
            .Select(tool => new AiToolDefinition
            {
                Name = tool.Name,
                Description = tool.Description,
                ParametersJsonSchema = tool.ParametersJsonSchema
            })
            .ToArray();
        _options = options ?? new CodebaseAssertionOptions();
    }

    internal async Task<AiAssertionResult> EvaluateAsync(string requirement, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requirement);

        var traceRecorder = _options.ExecutionTraceEnabled
            ? new AiAssertionExecutionTraceRecorder()
            : null;
        var client = traceRecorder is null
            ? _client
            : new ExecutionTraceRecordingClient(_client, traceRecorder);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (_options.Timeout > TimeSpan.Zero)
            timeoutCts.CancelAfter(_options.Timeout);

        AiAssertionResult result;

        try
        {
            result = await EvaluateCoreAsync(requirement, client, traceRecorder, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested
            && _options.Timeout > TimeSpan.Zero
            && timeoutCts.IsCancellationRequested)
        {
            result = new AiAssertionResult
            {
                Passed = false,
                Confidence = 0,
                Reason = $"Timed out after {_options.Timeout}.",
                Evidence = [],
                MissingEvidence = [],
                IsConclusive = false
            };
        }

        if (traceRecorder is null)
            return result;

        traceRecorder.Record(
            AiAssertionExecutionTraceEntryKind.RunCompleted,
            "assertion",
            new
            {
                result.Passed,
                result.Confidence,
                result.IsConclusive,
                result.Reason
            });

        return result with { ExecutionTrace = traceRecorder.Snapshot() };
    }

    private async Task<AiAssertionResult> EvaluateCoreAsync(
        string requirement,
        IToolCallingClient client,
        AiAssertionExecutionTraceRecorder? traceRecorder,
        CancellationToken cancellationToken)
    {
        var workingDirectory = _options.WorkingDirectory ?? Directory.GetCurrentDirectory();
        var codebaseRoot = PathSafety.DiscoverRoot(workingDirectory);
        var context = new ToolExecutionContext(workingDirectory, codebaseRoot);
        var userMessage = await BuildUserMessageAsync(requirement, codebaseRoot, cancellationToken).ConfigureAwait(false);

        var messages = new List<AiChatMessage>
        {
            new()
            {
                Role = "system",
                Content = BuildSystemPrompt()
            },
            new()
            {
                Role = "user",
                Content = userMessage
            }
        };
        var conversationCheckpoint = new CodebaseConversationCheckpoint();

        for (var step = 0; step < _options.MaxToolIterations; step++)
        {
            var compactionActivity = traceRecorder?.Begin();
            var previousRevision = conversationCheckpoint.Revision;
            var requestMessages = await CodebaseConversationCompactor
                .BuildRequestMessagesAsync(
                    messages,
                    client,
                    conversationCheckpoint,
                    _options.ConversationCompactionEnabled,
                    _options.RecentToolCallTurns,
                    _options.MaxCompactedToolResultChars,
                    _options.MaxCompactedStateChars,
                    _options.MaxRequestTokens,
                    _options.RequestTokenEstimator,
                    cancellationToken)
                .ConfigureAwait(false);
            var compactedThroughMessageIndex = conversationCheckpoint.CompactedThroughMessageIndex;
            var removedMessageCount = Math.Min(
                Math.Max(compactedThroughMessageIndex - 2, 0),
                Math.Max(messages.Count - 2, 0));
            conversationCheckpoint.PruneCompactedPrefix(messages);

            if (traceRecorder is not null
                && compactionActivity is not null
                && conversationCheckpoint.Revision != previousRevision)
                traceRecorder.Complete(
                    compactionActivity.Value,
                    AiAssertionExecutionTraceEntryKind.ConversationCompaction,
                    $"revision_{conversationCheckpoint.Revision}",
                    new
                    {
                        iteration = step + 1,
                        revision = conversationCheckpoint.Revision,
                        compacted_through_message_index = compactedThroughMessageIndex,
                        removed_message_count = removedMessageCount,
                        semantic_summary = conversationCheckpoint.SemanticSummary
                    });

            var response = await client
                .GetToolResponseAsync(
                    new AiToolRequest
                    {
                        Messages = requestMessages,
                        Tools = _toolDefinitions
                    }, cancellationToken)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            if (response.ToolCalls.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(response.Content))
                    return new AiAssertionResult
                    {
                        Passed = false,
                        Confidence = 0,
                        Reason = "The model returned neither a verdict nor a tool call.",
                        Evidence = [],
                        MissingEvidence = [],
                        IsConclusive = false
                    };

                var result = AssertionJson.ParseVerdict(response.Content);

                return result;
            }

            messages.Add(new AiChatMessage
            {
                Role = "assistant",
                Content = response.Content ?? string.Empty,
                ToolCalls = response.ToolCalls
            });

            var toolMessages = await Task
                .WhenAll(response.ToolCalls.Select(call => ExecuteToolCallAsync(call, context, traceRecorder, cancellationToken)))
                .ConfigureAwait(false);

            messages.AddRange(toolMessages);
        }

        var recentMessages = new StringBuilder();

        foreach (var message in messages.TakeLast(8))
        {
            recentMessages.AppendLine(message.Role);
            recentMessages.AppendLine(message.Content);
        }

        return new AiAssertionResult
        {
            Passed = false,
            Confidence = 0,
            Reason = $"Exceeded {_options.MaxToolIterations} tool iterations.",
            Evidence = [],
            MissingEvidence =
            [
                new AiAssertionMissingEvidence
                {
                    Description = "The model did not reach a verdict before the tool iteration limit.",
                    ExpectedLocation = recentMessages.ToString()
                }
            ],
            IsConclusive = false
        };
    }

    private string BuildSystemPrompt()
    {
        var systemPrompt = _options.SystemPrompt ?? DefaultSystemPrompt;

        return string.IsNullOrWhiteSpace(_options.AdditionalSystemPrompt)
            ? systemPrompt
            : string.Concat(systemPrompt.TrimEnd(), Environment.NewLine, Environment.NewLine, _options.AdditionalSystemPrompt.Trim());
    }

    private async Task<string> BuildUserMessageAsync(string requirement, string codebaseRoot, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Requirement:");
        builder.AppendLine(requirement.Trim());
        builder.AppendLine();
        builder.AppendLine("Execution context:");
        builder.AppendLine("```json");
        builder.AppendLine(JsonSerializer.Serialize(new { codebase_root = codebaseRoot }, AssertionJson.Options));
        builder.AppendLine("```");

        var includedEvidence = await ReadIncludedEvidenceAsync(codebaseRoot, cancellationToken).ConfigureAwait(false);

        if (includedEvidence.Length <= 0)
            return builder.ToString();

        builder.AppendLine();
        builder.AppendLine("Pre-included code evidence:");
        builder.Append(includedEvidence);

        return builder.ToString();
    }

    private async Task<string> ReadIncludedEvidenceAsync(string codebaseRoot, CancellationToken cancellationToken)
    {
        var files = ResolveIncludedFiles(codebaseRoot).Take(20).ToArray();

        if (files.Length == 0)
            return string.Empty;

        var root = codebaseRoot;
        var builder = new StringBuilder();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var text = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);

            if (text.Length > 30_000)
                text = text[..30_000];

            builder.AppendLine($"File: {Path.GetRelativePath(root, file)}");
            builder.AppendLine($"```{GetMarkdownLanguage(file)}");
            builder.AppendLine(text);
            builder.AppendLine("```");
        }

        return builder.ToString();
    }

    private static string GetMarkdownLanguage(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".cs" => "csharp",
            ".fs" => "fsharp",
            ".vb" => "vbnet",
            ".js" => "javascript",
            ".jsx" => "jsx",
            ".ts" => "typescript",
            ".tsx" => "tsx",
            ".java" => "java",
            ".kt" => "kotlin",
            ".py" => "python",
            ".go" => "go",
            ".rs" => "rust",
            ".php" => "php",
            ".rb" => "ruby",
            ".swift" => "swift",
            ".sql" => "sql",
            ".json" => "json",
            ".xml" => "xml",
            ".xaml" => "xml",
            ".yaml" or ".yml" => "yaml",
            ".html" or ".htm" => "html",
            ".css" => "css",
            ".scss" => "scss",
            ".sh" => "bash",
            ".ps1" => "powershell",
            ".md" => "markdown",
            ".csproj" or ".fsproj" or ".vbproj" or ".props" or ".targets" => "xml",
            _ => string.Empty
        };

    private IReadOnlyList<string> ResolveIncludedFiles(string workingDirectory)
    {
        var root = Path.GetFullPath(workingDirectory);
        var files = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var include in _options.IncludedPaths)
        {
            var fullPath = Path.GetFullPath(Path.Combine(root, include));
            if (!fullPath.Equals(root, StringComparison.Ordinal) && !fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                continue;

            if (File.Exists(fullPath))
                files.Add(fullPath);
            else if (Directory.Exists(fullPath))
                foreach (var file in Directory.EnumerateFiles(fullPath, "*.cs", SearchOption.AllDirectories))
                    if (!file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                        files.Add(file);
        }

        foreach (var typeName in _options.IncludedTypes)
            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
                if (!file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && File.ReadLines(file).Any(line => line.Contains($"class {typeName}", StringComparison.Ordinal)
                        || line.Contains($"record {typeName}", StringComparison.Ordinal)
                        || line.Contains($"interface {typeName}", StringComparison.Ordinal)
                        || line.Contains($"struct {typeName}", StringComparison.Ordinal)))
                    files.Add(file);

        return files.ToArray();
    }

    private static async Task<string> ExecuteToolAsync(
        IAiTool tool,
        string argumentsJson,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            return await tool
                .ExecuteAsync(argumentsJson, context, cancellationToken)
                .AsTask()
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, AssertionJson.Options);
        }
    }

    private async Task<AiChatMessage> ExecuteToolCallAsync(
        AiToolCall call,
        ToolExecutionContext context,
        AiAssertionExecutionTraceRecorder? traceRecorder,
        CancellationToken cancellationToken)
    {
        var activity = traceRecorder?.Begin();
        string content;
        var cacheHit = false;

        try
        {
            if (_toolsByName.TryGetValue(call.Name, out var tool))
            {
                var cacheKey = CreateToolCacheKey(call);
                var cachedResult = await context
                    .GetOrAddToolResultAsync(
                        cacheKey,
                        () => ExecuteToolAsync(tool, call.ArgumentsJson, context, cancellationToken),
                        IsSuccessfulToolResult)
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);

                cacheHit = cachedResult.CacheHit;
                content = cacheHit
                    ? MarkToolResultAsCached(cachedResult.Content)
                    : cachedResult.Content;
            }
            else
            {
                content = JsonSerializer.Serialize(new { error = $"Unknown tool '{call.Name}'." }, AssertionJson.Options);
            }
        }
        catch (Exception exception)
        {
            if (traceRecorder is not null && activity is not null)
                traceRecorder.Complete(
                    activity.Value,
                    AiAssertionExecutionTraceEntryKind.ToolExecution,
                    call.Name,
                    new
                    {
                        tool_call_id = call.Id,
                        arguments = ParseTraceJson(call.ArgumentsJson),
                        error = exception.ToString()
                    });

            throw;
        }

        if (traceRecorder is not null && activity is not null)
            traceRecorder.Complete(
                activity.Value,
                AiAssertionExecutionTraceEntryKind.ToolExecution,
                call.Name,
                new
                {
                    tool_call_id = call.Id,
                    arguments = ParseTraceJson(call.ArgumentsJson),
                    result = ParseTraceJson(content),
                    cache_hit = cacheHit
                });

        return new AiChatMessage
        {
            Role = "tool",
            Content = content,
            Name = call.Name,
            ToolCallId = call.Id
        };
    }

    private static string CreateToolCacheKey(AiToolCall call) =>
        string.Concat(call.Name, "\n", CanonicalizeJson(call.ArgumentsJson));

    private static JsonNode ParseTraceJson(string json)
    {
        try
        {
            return JsonNode.Parse(json) ?? JsonValue.Create(json);
        }
        catch (JsonException)
        {
            return JsonValue.Create(json);
        }
    }

    private static bool IsSuccessfulToolResult(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            return document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("error", out _);
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private static string CanonicalizeJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var builder = new StringBuilder();
            WriteCanonicalJson(document.RootElement, builder);
            return builder.ToString();
        }
        catch (JsonException)
        {
            return json.Trim();
        }
    }

    private static void WriteCanonicalJson(JsonElement element, StringBuilder builder)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                builder.Append('{');
                var firstProperty = true;
                foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    if (!firstProperty)
                        builder.Append(',');

                    firstProperty = false;
                    builder.Append(JsonSerializer.Serialize(property.Name, AssertionJson.Options));
                    builder.Append(':');
                    WriteCanonicalJson(property.Value, builder);
                }

                builder.Append('}');
                break;
            case JsonValueKind.Array:
                builder.Append('[');
                var firstItem = true;
                foreach (var item in element.EnumerateArray())
                {
                    if (!firstItem)
                        builder.Append(',');

                    firstItem = false;
                    WriteCanonicalJson(item, builder);
                }

                builder.Append(']');
                break;
            default:
                builder.Append(element.GetRawText());
                break;
        }
    }

    private static string MarkToolResultAsCached(string content)
    {
        try
        {
            var node = JsonNode.Parse(content);
            if (node is JsonObject result)
            {
                result["cached"] = true;
                return result.ToJsonString(AssertionJson.Options);
            }

            return JsonSerializer.Serialize(new { cached = true, result = node }, AssertionJson.Options);
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(new { cached = true, result = content }, AssertionJson.Options);
        }
    }
}
