using System.Text.Json;
using AiAssertions.Core.Abstractions;
using AiAssertions.Core.Agent;
using AiAssertions.Core.Assertions;
using AiAssertions.Core.Models;

namespace AiAssertions;

/// <summary>
/// Fluent builder for configuring and executing an AI-powered codebase assertion.
/// </summary>
public sealed class CodebaseAssertion
{
    private static readonly JsonSerializerOptions ExecutionTraceJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IToolCallingClient _client;
    private readonly IReadOnlyList<string> _includedPaths;
    private readonly IReadOnlyList<string> _includedTypes;
    private readonly string? _systemPrompt;
    private readonly string? _additionalSystemPrompt;
    private readonly int _maxToolIterations;
    private readonly int? _maxRequestTokens;
    private readonly Func<IReadOnlyList<AiChatMessage>, int>? _requestTokenEstimator;
    private readonly bool _conversationCompactionEnabled;
    private readonly int _recentToolCallTurns;
    private readonly int _maxCompactedToolResultChars;
    private readonly int _maxCompactedStateChars;
    private readonly double _minimumFalseConfidence;
    private readonly double _minimumTrueConfidence;
    private readonly TimeSpan _timeout;
    private readonly bool _executionTraceEnabled;

    internal CodebaseAssertion(IToolCallingClient client, AiAssertDefaults defaults)
        : this(
            client,
            [],
            [],
            defaults.SystemPrompt,
            defaults.AdditionalSystemPrompt,
            defaults.Timeout,
            defaults.MaxToolIterations,
            defaults.MaxRequestTokens,
            defaults.RequestTokenEstimator,
            defaults.ConversationCompactionEnabled,
            defaults.RecentToolCallTurns,
            defaults.MaxCompactedToolResultChars,
            defaults.MaxCompactedStateChars,
            defaults.MinimumTrueConfidence,
            defaults.MinimumFalseConfidence,
            defaults.ExecutionTraceEnabled)
    {
    }

    private CodebaseAssertion(
        IToolCallingClient client,
        IReadOnlyList<string> includedPaths,
        IReadOnlyList<string> includedTypes,
        string? systemPrompt,
        string? additionalSystemPrompt,
        TimeSpan timeout,
        int maxToolIterations,
        int? maxRequestTokens,
        Func<IReadOnlyList<AiChatMessage>, int>? requestTokenEstimator,
        bool conversationCompactionEnabled,
        int recentToolCallTurns,
        int maxCompactedToolResultChars,
        int maxCompactedStateChars,
        double minimumTrueConfidence,
        double minimumFalseConfidence,
        bool executionTraceEnabled)
    {
        _client = client;
        _includedPaths = includedPaths;
        _includedTypes = includedTypes;
        _systemPrompt = systemPrompt;
        _additionalSystemPrompt = additionalSystemPrompt;
        _timeout = timeout;
        _maxToolIterations = maxToolIterations;
        _maxRequestTokens = maxRequestTokens;
        _requestTokenEstimator = requestTokenEstimator;
        _conversationCompactionEnabled = conversationCompactionEnabled;
        _recentToolCallTurns = recentToolCallTurns;
        _maxCompactedToolResultChars = maxCompactedToolResultChars;
        _maxCompactedStateChars = maxCompactedStateChars;
        _minimumTrueConfidence = minimumTrueConfidence;
        _minimumFalseConfidence = minimumFalseConfidence;
        _executionTraceEnabled = executionTraceEnabled;
    }

    /// <summary>
    /// Sets the maximum time allowed for the assertion agent to reach a result.
    /// </summary>
    /// <param name="timeout">The timeout for the assertion run.</param>
    /// <returns>A new assertion builder with the timeout configured.</returns>
    public CodebaseAssertion WithTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive.");

        return Clone(timeout: timeout);
    }

    /// <summary>
    /// Sets the maximum number of tool-calling iterations allowed for the assertion agent.
    /// </summary>
    /// <param name="maxToolIterations">The maximum number of tool-calling iterations.</param>
    /// <returns>A new assertion builder with the iteration limit configured.</returns>
    public CodebaseAssertion WithMaxToolIterations(int maxToolIterations)
    {
        if (maxToolIterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxToolIterations), "Max tool iterations must be positive.");

        return Clone(maxToolIterations: maxToolIterations);
    }

    /// <summary>
    /// <para>
    /// <c>EXPERIMENTAL</c>
    /// </para>
    /// Limits the approximate number of message tokens sent with each model request.
    /// Older conversation history is omitted when necessary.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The system prompt and initial user message are always preserved, even when they exceed the limit. The newest
    /// messages are then kept while older messages are omitted. Tool calls and their results are kept or omitted
    /// together to preserve valid conversation history.
    /// </para>
    /// <para>
    /// This is a best-effort limit based on the configured token estimator. Tool definitions and provider-specific
    /// request overhead are not included.
    /// </para>
    /// <para>
    /// This overload preserves an estimator inherited from global configuration. Use the overload with an explicit
    /// <see langword="null"/> estimator to select the built-in approximation.
    /// </para>
    /// </remarks>
    /// <param name="maxTokens">The approximate message-token limit for each model request.</param>
    /// <returns>A new assertion builder with the token limit configured.</returns>
    public CodebaseAssertion WithApproximateTokenLimit(int maxTokens)
    {
        if (maxTokens <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTokens), "Token limit must be positive.");

        return Clone(maxRequestTokens: maxTokens);
    }

    /// <summary>
    /// Limits the approximate number of message tokens sent with each model request and selects the token estimator.
    /// </summary>
    /// <param name="maxTokens">The approximate message-token limit for each model request.</param>
    /// <param name="tokenEstimator">
    /// A custom token estimator, or <see langword="null"/> to explicitly use the built-in approximation.
    /// </param>
    /// <returns>A new assertion builder with the token limit and estimator configured.</returns>
    public CodebaseAssertion WithApproximateTokenLimit(
        int maxTokens,
        Func<IReadOnlyList<AiChatMessage>, int>? tokenEstimator)
    {
        if (maxTokens <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTokens), "Token limit must be positive.");

        return Clone(
            maxRequestTokens: maxTokens,
            requestTokenEstimator: tokenEstimator,
            resetRequestTokenEstimator: tokenEstimator is null);
    }

    /// <summary>
    /// Disables adaptive checkpointing of older conversation history.
    /// </summary>
    /// <remarks>
    /// <para>
    /// By default, history is kept intact until it approaches the configured budget. The model then creates a semantic
    /// checkpoint while recent tool-call turns are kept unchanged. Disabling compaction sends the complete history to
    /// the model and prevents checkpoint requests.
    /// </para>
    /// <para>
    /// If <see cref="WithApproximateTokenLimit(int)"/> is configured, older messages may still be omitted to satisfy
    /// that limit.
    /// </para>
    /// </remarks>
    /// <returns>A new assertion builder with conversation compaction disabled.</returns>
    public CodebaseAssertion WithoutConversationCompaction() =>
        Clone(conversationCompactionEnabled: false);

    /// <summary>
    /// Configures adaptive conversation checkpointing.
    /// </summary>
    /// <param name="options">Checkpointing options.</param>
    /// <returns>A new assertion builder with conversation checkpointing configured.</returns>
    public CodebaseAssertion WithConversationCompaction(ConversationCompactionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        return Clone(
            conversationCompactionEnabled: true,
            recentToolCallTurns: options.RecentToolCallTurns,
            maxCompactedStateChars: options.MaxCheckpointChars);
    }

    /// <summary>
    /// Enables collection of a complete execution trace for this assertion run.
    /// </summary>
    /// <remarks>
    /// Trace collection is disabled by default. Enabling it may increase execution time
    /// and memory usage.
    /// Traces may contain sensitive codebase data.
    /// </remarks>
    /// <returns>A new assertion builder with execution-trace collection enabled.</returns>
    public CodebaseAssertion WithExecutionTrace() =>
        Clone(executionTraceEnabled: true);

    /// <summary>
    /// Sets the minimum confidence required for both passing and failing verdicts.
    /// </summary>
    /// <param name="minimumConfidence">The minimum confidence value between 0 and 1.</param>
    /// <returns>A new assertion builder with the confidence tolerance configured.</returns>
    public CodebaseAssertion WithConfidenceTolerance(double minimumConfidence) =>
        WithConfidenceTolerance(minimumConfidence, minimumConfidence);

    /// <summary>
    /// Sets separate minimum confidence thresholds for passing and failing verdicts.
    /// </summary>
    /// <param name="minimumTrueConfidence">The minimum confidence required for a passed verdict.</param>
    /// <param name="minimumFalseConfidence">The minimum confidence required for a failed verdict.</param>
    /// <returns>A new assertion builder with the confidence tolerances configured.</returns>
    public CodebaseAssertion WithConfidenceTolerance(double minimumTrueConfidence, double minimumFalseConfidence)
    {
        ValidateConfidence(minimumTrueConfidence, nameof(minimumTrueConfidence));
        ValidateConfidence(minimumFalseConfidence, nameof(minimumFalseConfidence));

        return Clone(
            minimumTrueConfidence: minimumTrueConfidence,
            minimumFalseConfidence: minimumFalseConfidence);
    }

    /// <summary>
    /// Includes all C# files under a directory as initial evidence for the model.
    /// </summary>
    /// <param name="path">The directory path relative to the project root or working directory.</param>
    /// <returns>A new assertion builder with the directory included.</returns>
    public CodebaseAssertion IncludeDirectory(string path) =>
        IncludePath(path);

    /// <summary>
    /// Includes a file as initial evidence for the model.
    /// </summary>
    /// <param name="path">The file path relative to the project root or working directory.</param>
    /// <returns>A new assertion builder with the file included.</returns>
    public CodebaseAssertion IncludeFile(string path) =>
        IncludePath(path);

    /// <summary>
    /// Includes the source file that declares the specified type as initial evidence for the model.
    /// </summary>
    /// <typeparam name="T">The type whose declaration should be included.</typeparam>
    /// <returns>A new assertion builder with the type declaration included.</returns>
    public CodebaseAssertion IncludeType<T>() =>
        IncludeType(typeof(T).Name);

    /// <summary>
    /// Includes the source file that declares a type with the specified name as initial evidence for the model.
    /// </summary>
    /// <param name="typeName">The type name to locate in the codebase.</param>
    /// <returns>A new assertion builder with the type declaration included.</returns>
    public CodebaseAssertion IncludeType(string typeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        return Clone(includedTypes: [.. _includedTypes, typeName]);
    }

    /// <summary>
    /// Evaluates whether the codebase satisfies the supplied natural-language requirement.
    /// </summary>
    /// <param name="requirement">The business or architectural requirement to evaluate.</param>
    /// <returns>The assertion result, including verdict, confidence, comment, and evidence.</returns>
    public Task<CodebaseAssertionResult> That(string requirement) =>
        That(requirement, CancellationToken.None);

    /// <summary>
    /// Evaluates whether the codebase satisfies the supplied natural-language requirement.
    /// </summary>
    /// <param name="requirement">The business or architectural requirement to evaluate.</param>
    /// <param name="cancellationToken">A token used to cancel the assertion run.</param>
    /// <returns>The assertion result, including verdict, confidence, comment, and evidence.</returns>
    public Task<CodebaseAssertionResult> That(string requirement, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requirement);

        return ExecuteAsync(requirement, _timeout, cancellationToken);
    }

    /// <summary>
    /// Evaluates whether the codebase satisfies the natural-language requirement read from a text or Markdown file.
    /// </summary>
    /// <param name="path">The .txt or .md file path, absolute or relative to the current working directory.</param>
    /// <returns>The assertion result, including verdict, confidence, comment, and evidence.</returns>
    public Task<CodebaseAssertionResult> AgainstRequirementFile(string path) =>
        AgainstRequirementFile(path, CancellationToken.None);

    /// <summary>
    /// Evaluates whether the codebase satisfies the natural-language requirement read from a text or Markdown file.
    /// </summary>
    /// <param name="path">The .txt or .md file path, absolute or relative to the current working directory.</param>
    /// <param name="cancellationToken">A token used to cancel file reading and the assertion run.</param>
    /// <returns>The assertion result, including verdict, confidence, comment, and evidence.</returns>
    public async Task<CodebaseAssertionResult> AgainstRequirementFile(string path, CancellationToken cancellationToken)
    {
        var requirement = await ReadRequirementFileAsync(path, cancellationToken).ConfigureAwait(false);

        return await That(requirement, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CodebaseAssertionResult> ExecuteAsync(
        string requirement,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var result = await new CodebaseAssertionEngine(_client, options: new CodebaseAssertionOptions
        {
            IncludedPaths = _includedPaths,
            IncludedTypes = _includedTypes,
            SystemPrompt = _systemPrompt,
            AdditionalSystemPrompt = _additionalSystemPrompt,
            Timeout = timeout,
            MaxToolIterations = _maxToolIterations,
            MaxRequestTokens = _maxRequestTokens,
            RequestTokenEstimator = _requestTokenEstimator,
            ConversationCompactionEnabled = _conversationCompactionEnabled,
            RecentToolCallTurns = _recentToolCallTurns,
            MaxCompactedToolResultChars = _maxCompactedToolResultChars,
            MaxCompactedStateChars = _maxCompactedStateChars,
            MinimumTrueConfidence = _minimumTrueConfidence,
            MinimumFalseConfidence = _minimumFalseConfidence,
            ExecutionTraceEnabled = _executionTraceEnabled
        })
            .EvaluateAsync(requirement, cancellationToken)
            .ConfigureAwait(false);

        var verdict = ToVerdict(result);
        var comment = result.Reason;
        if (verdict == CodebaseAssertionVerdict.NotDetermined && result.IsConclusive)
            comment = $"Model confidence {result.Confidence:0.00} is below configured tolerance. {comment}";

        return new CodebaseAssertionResult
        {
            Verdict = verdict,
            Confidence = result.Confidence,
            Comment = comment,
            Evidence = result.Evidence
                .Select(evidence => new CodebaseAssertionEvidence
                {
                    File = evidence.File,
                    StartLine = evidence.StartLine,
                    EndLine = evidence.EndLine,
                    Description = evidence.Description
                })
                .ToArray(),
            MissingEvidence = result.MissingEvidence
                .Select(evidence => new CodebaseAssertionMissingEvidence
                {
                    Description = evidence.Description,
                    ExpectedLocation = evidence.ExpectedLocation
                })
                .ToArray(),
            ExecutionTrace = result.ExecutionTrace is null
                ? null
                : ToExecutionTrace(result.ExecutionTrace, result, verdict, comment)
        };
    }

    private CodebaseAssertion IncludePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Clone([.. _includedPaths, path]);
    }

    private static async Task<string> ReadRequirementFileAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var extension = Path.GetExtension(path);

        if (!extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".md", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Requirement file must have .txt or .md extension.", nameof(path));

        var fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Requirement file was not found.", fullPath);

        var requirement = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);

        ArgumentException.ThrowIfNullOrWhiteSpace(requirement, nameof(path));

        return requirement;
    }

    private CodebaseAssertion Clone(
        IReadOnlyList<string>? includedPaths = null,
        IReadOnlyList<string>? includedTypes = null,
        TimeSpan? timeout = null,
        int? maxToolIterations = null,
        int? maxRequestTokens = null,
        Func<IReadOnlyList<AiChatMessage>, int>? requestTokenEstimator = null,
        bool resetRequestTokenEstimator = false,
        bool? conversationCompactionEnabled = null,
        int? recentToolCallTurns = null,
        int? maxCompactedToolResultChars = null,
        int? maxCompactedStateChars = null,
        double? minimumTrueConfidence = null,
        double? minimumFalseConfidence = null,
        bool? executionTraceEnabled = null) =>
        new(
            _client,
            includedPaths ?? _includedPaths,
            includedTypes ?? _includedTypes,
            _systemPrompt,
            _additionalSystemPrompt,
            timeout ?? _timeout,
            maxToolIterations ?? _maxToolIterations,
            maxRequestTokens ?? _maxRequestTokens,
            resetRequestTokenEstimator ? null : requestTokenEstimator ?? _requestTokenEstimator,
            conversationCompactionEnabled ?? _conversationCompactionEnabled,
            recentToolCallTurns ?? _recentToolCallTurns,
            maxCompactedToolResultChars ?? _maxCompactedToolResultChars,
            maxCompactedStateChars ?? _maxCompactedStateChars,
            minimumTrueConfidence ?? _minimumTrueConfidence,
            minimumFalseConfidence ?? _minimumFalseConfidence,
            executionTraceEnabled ?? _executionTraceEnabled);

    private CodebaseAssertionVerdict ToVerdict(AiAssertionResult result)
    {
        if (!result.IsConclusive)
            return CodebaseAssertionVerdict.NotDetermined;

        if (result.Passed)
            return result.Confidence >= _minimumTrueConfidence
                ? CodebaseAssertionVerdict.Passed
                : CodebaseAssertionVerdict.NotDetermined;

        return result.Confidence >= _minimumFalseConfidence
            ? CodebaseAssertionVerdict.Failed
            : CodebaseAssertionVerdict.NotDetermined;
    }

    private static CodebaseAssertionExecutionTraceEntry ToExecutionTraceEntry(AiAssertionExecutionTraceEntry entry)
    {
        using var document = JsonDocument.Parse(entry.PayloadJson);
        var payload = document.RootElement;

        return entry.Kind switch
        {
            AiAssertionExecutionTraceEntryKind.ModelExchange => CreateModelExchangeTraceEntry(entry, payload),
            AiAssertionExecutionTraceEntryKind.ConversationCompactionModelExchange =>
                CreateCompactionModelExchangeTraceEntry(entry, payload),
            AiAssertionExecutionTraceEntryKind.ConversationCompaction =>
                CreateConversationCompactionTraceEntry(entry, payload),
            AiAssertionExecutionTraceEntryKind.ToolExecution => CreateToolExecutionTraceEntry(entry, payload),
            AiAssertionExecutionTraceEntryKind.ModelVerdictReceived =>
                CreateModelVerdictReceivedTraceEntry(entry, payload),
            _ => throw new ArgumentOutOfRangeException(nameof(entry), entry.Kind, "Unknown execution trace entry kind.")
        };
    }

    private CodebaseAssertionExecutionTrace ToExecutionTrace(
        AiAssertionExecutionTrace trace,
        AiAssertionResult result,
        CodebaseAssertionVerdict verdict,
        string comment)
    {
        var entries = trace.Entries.Select(ToExecutionTraceEntry).ToList();
        var completedAtUtc = DateTimeOffset.UtcNow;
        var modelVerdictReceived = trace.Entries.Any(
            entry => entry.Kind == AiAssertionExecutionTraceEntryKind.ModelVerdictReceived);
        double? appliedConfidenceThreshold = modelVerdictReceived && result.IsConclusive
            ? result.Passed ? _minimumTrueConfidence : _minimumFalseConfidence
            : null;
        var decision = GetFinalVerdictDecision(modelVerdictReceived, result.IsConclusive, verdict);
        entries.Add(new CodebaseAssertionFinalVerdictTraceEntry
        {
            Sequence = entries.Count + 1,
            StartedAtUtc = completedAtUtc,
            Duration = TimeSpan.Zero,
            Verdict = verdict,
            Confidence = result.Confidence,
            ModelVerdictReceived = modelVerdictReceived,
            ModelPassed = modelVerdictReceived ? result.Passed : null,
            ModelIsConclusive = modelVerdictReceived ? result.IsConclusive : null,
            AppliedConfidenceThreshold = appliedConfidenceThreshold,
            Decision = decision,
            Comment = comment
        });

        return new CodebaseAssertionExecutionTrace
        {
            StartedAtUtc = trace.StartedAtUtc,
            CompletedAtUtc = completedAtUtc,
            Duration = completedAtUtc - trace.StartedAtUtc,
            Entries = entries
        };
    }

    private static CodebaseAssertionFinalVerdictDecision GetFinalVerdictDecision(
        bool modelVerdictReceived,
        bool modelIsConclusive,
        CodebaseAssertionVerdict verdict)
    {
        if (!modelVerdictReceived)
            return CodebaseAssertionFinalVerdictDecision.NoModelVerdict;

        if (!modelIsConclusive)
            return CodebaseAssertionFinalVerdictDecision.ModelInconclusive;

        return verdict == CodebaseAssertionVerdict.NotDetermined
            ? CodebaseAssertionFinalVerdictDecision.BelowConfidenceThreshold
            : CodebaseAssertionFinalVerdictDecision.Accepted;
    }

    private static CodebaseAssertionModelExchangeTraceEntry CreateModelExchangeTraceEntry(
        AiAssertionExecutionTraceEntry entry,
        JsonElement payload) =>
        new()
        {
            Sequence = entry.Sequence,
            StartedAtUtc = entry.StartedAtUtc,
            Duration = entry.Duration,
            Request = DeserializeRequiredTraceProperty<AiToolRequest>(payload, "request"),
            RequestMetadata = DeserializeOptionalTraceProperty<AiModelRequestMetadata>(payload, "requestMetadata"),
            Response = DeserializeOptionalTraceProperty<AiToolResponse>(payload, "response"),
            Error = GetOptionalTraceString(payload, "error")
        };

    private static CodebaseAssertionCompactionModelExchangeTraceEntry CreateCompactionModelExchangeTraceEntry(
        AiAssertionExecutionTraceEntry entry,
        JsonElement payload) =>
        new()
        {
            Sequence = entry.Sequence,
            StartedAtUtc = entry.StartedAtUtc,
            Duration = entry.Duration,
            Request = DeserializeRequiredTraceProperty<AiTextRequest>(payload, "request"),
            RequestMetadata = DeserializeOptionalTraceProperty<AiModelRequestMetadata>(payload, "requestMetadata"),
            Response = DeserializeOptionalTraceProperty<AiTextResponse>(payload, "response"),
            Error = GetOptionalTraceString(payload, "error")
        };

    private static CodebaseAssertionConversationCompactionTraceEntry CreateConversationCompactionTraceEntry(
        AiAssertionExecutionTraceEntry entry,
        JsonElement payload) =>
        new()
        {
            Sequence = entry.Sequence,
            StartedAtUtc = entry.StartedAtUtc,
            Duration = entry.Duration,
            Iteration = payload.GetProperty("iteration").GetInt32(),
            Revision = payload.GetProperty("revision").GetInt32(),
            CompactedThroughMessageIndex = payload.GetProperty("compacted_through_message_index").GetInt32(),
            RemovedMessageCount = payload.GetProperty("removed_message_count").GetInt32(),
            SemanticSummary = GetRequiredTraceString(payload, "semantic_summary")
        };

    private static CodebaseAssertionToolExecutionTraceEntry CreateToolExecutionTraceEntry(
        AiAssertionExecutionTraceEntry entry,
        JsonElement payload) =>
        new()
        {
            Sequence = entry.Sequence,
            StartedAtUtc = entry.StartedAtUtc,
            Duration = entry.Duration,
            ToolCallId = GetRequiredTraceString(payload, "tool_call_id"),
            ToolName = entry.Name,
            Arguments = payload.GetProperty("arguments").Clone(),
            Result = GetOptionalTraceElement(payload, "result"),
            CacheHit = payload.TryGetProperty("cache_hit", out var cacheHit) && cacheHit.GetBoolean(),
            Error = GetOptionalTraceString(payload, "error")
        };

    private static CodebaseAssertionModelVerdictReceivedTraceEntry CreateModelVerdictReceivedTraceEntry(
        AiAssertionExecutionTraceEntry entry,
        JsonElement payload) =>
        new()
        {
            Sequence = entry.Sequence,
            StartedAtUtc = entry.StartedAtUtc,
            Duration = entry.Duration,
            Passed = payload.GetProperty("passed").GetBoolean(),
            Confidence = payload.GetProperty("confidence").GetDouble(),
            IsConclusive = payload.GetProperty("isConclusive").GetBoolean(),
            Reason = GetRequiredTraceString(payload, "reason"),
            ParsingError = GetOptionalTraceString(payload, "parsingError")
        };

    private static T DeserializeRequiredTraceProperty<T>(JsonElement payload, string propertyName)
        where T : class =>
        payload.GetProperty(propertyName).Deserialize<T>(ExecutionTraceJsonOptions)
        ?? throw new JsonException($"Execution trace property '{propertyName}' was null.");

    private static T? DeserializeOptionalTraceProperty<T>(JsonElement payload, string propertyName)
        where T : class =>
        payload.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.Deserialize<T>(ExecutionTraceJsonOptions)
            : null;

    private static JsonElement? GetOptionalTraceElement(JsonElement payload, string propertyName) =>
        payload.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.Clone()
            : null;

    private static string GetRequiredTraceString(JsonElement payload, string propertyName) =>
        payload.GetProperty(propertyName).GetString()
        ?? throw new JsonException($"Execution trace property '{propertyName}' was null.");

    private static string? GetOptionalTraceString(JsonElement payload, string propertyName) =>
        payload.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;

    private static void ValidateConfidence(double value, string parameterName)
    {
        if (value is < 0 or > 1)
            throw new ArgumentOutOfRangeException(parameterName, "Confidence must be between 0 and 1.");
    }
}
