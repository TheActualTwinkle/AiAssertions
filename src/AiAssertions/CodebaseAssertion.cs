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
    private readonly Func<IReadOnlyList<AiChatMessage>, IReadOnlyList<AiChatMessage>>? _conversationCompactor;
    private readonly double _minimumFalseConfidence;
    private readonly double _minimumTrueConfidence;
    private readonly TimeSpan _timeout;

    internal CodebaseAssertion(IToolCallingClient client, AiAssertDefaults defaults)
        : this(
            client,
            [],
            [],
            null,
            null,
            defaults.Timeout,
            defaults.MaxToolIterations,
            defaults.MaxRequestTokens,
            defaults.RequestTokenEstimator,
            defaults.ConversationCompactionEnabled,
            defaults.RecentToolCallTurns,
            defaults.MaxCompactedToolResultChars,
            defaults.ConversationCompactor,
            defaults.MinimumTrueConfidence,
            defaults.MinimumFalseConfidence)
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
        Func<IReadOnlyList<AiChatMessage>, IReadOnlyList<AiChatMessage>>? conversationCompactor,
        double minimumTrueConfidence,
        double minimumFalseConfidence)
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
        _conversationCompactor = conversationCompactor;
        _minimumTrueConfidence = minimumTrueConfidence;
        _minimumFalseConfidence = minimumFalseConfidence;
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
    /// <para>
    /// <c>EXPERIMENTAL</c>
    /// </para>
    /// Sets the function used to estimate the token count of messages before each model request.
    /// </summary>
    /// <remarks>
    /// The returned estimate is compared with the limit configured by
    /// <see cref="WithApproximateTokenLimit(int)"/>. Tool definitions and provider-specific request overhead are not
    /// passed to the estimator.
    /// </remarks>
    /// <param name="tokenEstimator">
    /// A function that receives the selected request messages and returns their estimated token count.
    /// </param>
    /// <returns>A new assertion builder with the token estimator configured.</returns>
    public CodebaseAssertion WithTokenEstimator(Func<IReadOnlyList<AiChatMessage>, int> tokenEstimator)
    {
        ArgumentNullException.ThrowIfNull(tokenEstimator);

        return Clone(requestTokenEstimator: tokenEstimator);
    }

    /// <summary>
    /// Replaces the codebase assertion agent's default system prompt.
    /// </summary>
    /// <param name="systemPrompt">The complete system prompt to send instead of the default prompt.</param>
    /// <returns>A new assertion builder with the replacement system prompt configured.</returns>
    public CodebaseAssertion WithSystemPrompt(string systemPrompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);

        return Clone(systemPrompt: systemPrompt);
    }

    /// <summary>
    /// Appends instructions to the system prompt.
    /// </summary>
    /// <remarks>
    /// Multiple calls append instructions in the order they are made.
    /// </remarks>
    /// <param name="additionalSystemPrompt">The instructions to append.</param>
    /// <returns>A new assertion builder with the additional instructions configured.</returns>
    public CodebaseAssertion WithAdditionalSystemPrompt(string additionalSystemPrompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(additionalSystemPrompt);

        var prompt = string.IsNullOrWhiteSpace(_additionalSystemPrompt)
            ? additionalSystemPrompt
            : string.Concat(_additionalSystemPrompt.TrimEnd(), Environment.NewLine, Environment.NewLine, additionalSystemPrompt.Trim());

        return Clone(additionalSystemPrompt: prompt);
    }

    /// <summary>
    /// Disables the default summarization of older conversation history.
    /// </summary>
    /// <remarks>
    /// <para>
    /// By default, older tool calls and results are summarized and truncated while recent tool-call turns are kept unchanged.
    /// Disabling compaction sends the complete history to the model.
    /// </para>
    /// <para>
    /// If <see cref="WithApproximateTokenLimit(int)"/> is configured, older messages may still be omitted to satisfy
    /// that limit.
    /// </para>
    /// </remarks>
    /// <returns>A new assertion builder with conversation compaction disabled.</returns>
    public CodebaseAssertion WithoutConversationCompaction() =>
        Clone(conversationCompactionEnabled: false, resetConversationCompactor: true);

    /// <summary>
    /// Replaces the default conversation compactor with a function that selects the messages sent with each model
    /// request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The function receives the complete conversation accumulated so far. It may summarize, remove, or reorder
    /// messages, but should preserve enough context for the model to continue the assertion.
    /// </para>
    /// <para>
    /// If <see cref="WithApproximateTokenLimit(int)"/> is configured, its limit is applied after this function.
    /// </para>
    /// </remarks>
    /// <param name="conversationCompactor">
    /// A function that receives the complete conversation and returns the messages for the next model request.
    /// </param>
    /// <returns>A new assertion builder with a custom conversation compactor configured.</returns>
    public CodebaseAssertion WithConversationCompactor(Func<IReadOnlyList<AiChatMessage>, IReadOnlyList<AiChatMessage>> conversationCompactor)
    {
        ArgumentNullException.ThrowIfNull(conversationCompactor);

        return Clone(
            conversationCompactionEnabled: true,
            conversationCompactor: conversationCompactor);
    }

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
            ConversationCompactor = _conversationCompactor,
            MinimumTrueConfidence = _minimumTrueConfidence,
            MinimumFalseConfidence = _minimumFalseConfidence
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
                .ToArray()
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
        string? systemPrompt = null,
        string? additionalSystemPrompt = null,
        TimeSpan? timeout = null,
        int? maxToolIterations = null,
        int? maxRequestTokens = null,
        Func<IReadOnlyList<AiChatMessage>, int>? requestTokenEstimator = null,
        bool? conversationCompactionEnabled = null,
        int? recentToolCallTurns = null,
        int? maxCompactedToolResultChars = null,
        Func<IReadOnlyList<AiChatMessage>, IReadOnlyList<AiChatMessage>>? conversationCompactor = null,
        bool resetConversationCompactor = false,
        double? minimumTrueConfidence = null,
        double? minimumFalseConfidence = null) =>
        new(
            _client,
            includedPaths ?? _includedPaths,
            includedTypes ?? _includedTypes,
            systemPrompt ?? _systemPrompt,
            additionalSystemPrompt ?? _additionalSystemPrompt,
            timeout ?? _timeout,
            maxToolIterations ?? _maxToolIterations,
            maxRequestTokens ?? _maxRequestTokens,
            requestTokenEstimator ?? _requestTokenEstimator,
            conversationCompactionEnabled ?? _conversationCompactionEnabled,
            recentToolCallTurns ?? _recentToolCallTurns,
            maxCompactedToolResultChars ?? _maxCompactedToolResultChars,
            resetConversationCompactor ? null : conversationCompactor ?? _conversationCompactor,
            minimumTrueConfidence ?? _minimumTrueConfidence,
            minimumFalseConfidence ?? _minimumFalseConfidence);

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

    private static void ValidateConfidence(double value, string parameterName)
    {
        if (value is < 0 or > 1)
            throw new ArgumentOutOfRangeException(parameterName, "Confidence must be between 0 and 1.");
    }
}
