using AiAssertions.Core.Models;

namespace AiAssertions;

/// <summary>
/// Fluent builder for configuring global AIAssert defaults.
/// </summary>
public sealed class AiAssertConfiguration
{
    private readonly Func<AiAssertDefaults> _getDefaults;
    private readonly Action<AiAssertDefaults> _setDefaults;

    internal AiAssertConfiguration(Func<AiAssertDefaults> getDefaults, Action<AiAssertDefaults> setDefaults)
    {
        _getDefaults = getDefaults;
        _setDefaults = setDefaults;
    }

    /// <summary>
    /// Sets the default timeout used by AIAssert assertions.
    /// </summary>
    /// <param name="timeout">The default assertion timeout.</param>
    /// <returns>The same configuration builder.</returns>
    public AiAssertConfiguration WithDefaultTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive.");

        var defaults = _getDefaults();
        _setDefaults(defaults with { Timeout = timeout });

        return this;
    }

    /// <summary>
    /// Sets the default maximum number of tool-calling iterations used by AIAssert assertions.
    /// </summary>
    /// <param name="maxToolIterations">The default maximum number of tool-calling iterations.</param>
    /// <returns>The same configuration builder.</returns>
    public AiAssertConfiguration WithDefaultMaxToolIterations(int maxToolIterations)
    {
        if (maxToolIterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxToolIterations), "Max tool iterations must be positive.");

        var defaults = _getDefaults();

        _setDefaults(defaults with { MaxToolIterations = maxToolIterations });

        return this;
    }

    /// <summary>
    /// <para>
    /// <c>EXPERIMENTAL</c>
    /// </para>
    /// Sets the global approximate message-token limit for each model request.
    /// </summary>
    /// <remarks>
    /// The system prompt and initial user message are always preserved. The limit is applied after conversation
    /// compaction and does not include tool definitions or provider-specific request overhead. This overload preserves
    /// an estimator configured by an earlier call. Use the overload with an explicit <see langword="null"/> estimator
    /// to select the built-in approximation.
    /// </remarks>
    /// <param name="maxTokens">The approximate message-token limit for each model request.</param>
    /// <returns>The same configuration builder.</returns>
    public AiAssertConfiguration WithGlobalApproximateTokenLimit(int maxTokens)
    {
        if (maxTokens <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTokens), "Token limit must be positive.");

        var defaults = _getDefaults();
        _setDefaults(defaults with { MaxRequestTokens = maxTokens });

        return this;
    }

    /// <summary>
    /// Sets the global approximate message-token limit and selects the token estimator.
    /// </summary>
    /// <param name="maxTokens">The approximate message-token limit for each model request.</param>
    /// <param name="tokenEstimator">
    /// A custom token estimator, or <see langword="null"/> to explicitly use the built-in approximation.
    /// </param>
    /// <returns>The same configuration builder.</returns>
    public AiAssertConfiguration WithGlobalApproximateTokenLimit(
        int maxTokens,
        Func<IReadOnlyList<AiChatMessage>, int>? tokenEstimator)
    {
        if (maxTokens <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTokens), "Token limit must be positive.");

        var defaults = _getDefaults();
        _setDefaults(defaults with
        {
            MaxRequestTokens = maxTokens,
            RequestTokenEstimator = tokenEstimator
        });

        return this;
    }

    /// <summary>
    /// Sets the global replacement for the codebase assertion agent's default system prompt.
    /// </summary>
    /// <remarks>
    /// This replaces the complete protocol prompt. Review the library's default system prompt before overriding it;
    /// otherwise required tool-use, pagination, checkpointing, and verdict instructions may be lost. Prefer
    /// <see cref="WithGlobalAdditionalSystemPrompt(string)"/> when you only need to add instructions.
    /// </remarks>
    /// <param name="systemPrompt">The complete system prompt to use for codebase assertions.</param>
    /// <returns>The same configuration builder.</returns>
    public AiAssertConfiguration WithGlobalSystemPrompt(string systemPrompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);

        var defaults = _getDefaults();
        _setDefaults(defaults with { SystemPrompt = systemPrompt });

        return this;
    }

    /// <summary>
    /// Appends global instructions to the system prompt used by codebase assertions.
    /// </summary>
    /// <remarks>
    /// Multiple calls append instructions in the order they are made.
    /// </remarks>
    /// <param name="additionalSystemPrompt">The instructions to append.</param>
    /// <returns>The same configuration builder.</returns>
    public AiAssertConfiguration WithGlobalAdditionalSystemPrompt(string additionalSystemPrompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(additionalSystemPrompt);

        var defaults = _getDefaults();
        var prompt = string.IsNullOrWhiteSpace(defaults.AdditionalSystemPrompt)
            ? additionalSystemPrompt
            : string.Concat(
                defaults.AdditionalSystemPrompt.TrimEnd(),
                Environment.NewLine,
                Environment.NewLine,
                additionalSystemPrompt.Trim());

        _setDefaults(defaults with { AdditionalSystemPrompt = prompt });

        return this;
    }

    /// <summary>
    /// Globally disables adaptive checkpointing of older conversation history.
    /// </summary>
    /// <remarks>
    /// A global approximate token limit may still omit older messages.
    /// </remarks>
    /// <returns>The same configuration builder.</returns>
    public AiAssertConfiguration WithoutGlobalConversationCompaction()
    {
        var defaults = _getDefaults();
        _setDefaults(defaults with { ConversationCompactionEnabled = false });

        return this;
    }

    /// <summary>
    /// Configures adaptive conversation checkpointing for subsequent assertions.
    /// </summary>
    /// <param name="options">Checkpointing options.</param>
    /// <returns>The same configuration builder.</returns>
    public AiAssertConfiguration WithGlobalConversationCompaction(ConversationCompactionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var defaults = _getDefaults();
        _setDefaults(defaults with
        {
            ConversationCompactionEnabled = true,
            RecentToolCallTurns = options.RecentToolCallTurns,
            MaxCompactedStateChars = options.MaxCheckpointChars
        });

        return this;
    }

    /// <summary>
    /// Enables collection of execution traces for subsequent codebase assertions.
    /// </summary>
    /// <remarks>
    /// Trace collection is disabled by default. Enabling it may increase execution time
    /// and memory usage.
    /// Traces may contain sensitive codebase data.
    /// </remarks>
    /// <returns>The same configuration builder.</returns>
    public AiAssertConfiguration WithGlobalExecutionTrace()
    {
        var defaults = _getDefaults();

        _setDefaults(defaults with { ExecutionTraceEnabled = true });

        return this;
    }

    /// <summary>
    /// Sets the default minimum confidence required for both passing and failing verdicts.
    /// </summary>
    /// <param name="minimumConfidence">The minimum confidence value between 0 and 1.</param>
    /// <returns>The same configuration builder.</returns>
    public AiAssertConfiguration WithGlobalConfidenceTolerance(double minimumConfidence) =>
        WithGlobalConfidenceTolerance(minimumConfidence, minimumConfidence);

    /// <summary>
    /// Sets separate default minimum confidence thresholds for passing and failing verdicts.
    /// </summary>
    /// <param name="minimumTrueConfidence">The minimum confidence required for a passed verdict.</param>
    /// <param name="minimumFalseConfidence">The minimum confidence required for a failed verdict.</param>
    /// <returns>The same configuration builder.</returns>
    public AiAssertConfiguration WithGlobalConfidenceTolerance(double minimumTrueConfidence, double minimumFalseConfidence)
    {
        ValidateConfidence(minimumTrueConfidence, nameof(minimumTrueConfidence));
        ValidateConfidence(minimumFalseConfidence, nameof(minimumFalseConfidence));

        var defaults = _getDefaults();
        _setDefaults(defaults with
        {
            MinimumTrueConfidence = minimumTrueConfidence,
            MinimumFalseConfidence = minimumFalseConfidence
        });

        return this;
    }

    private static void ValidateConfidence(double value, string parameterName)
    {
        if (value is < 0 or > 1)
            throw new ArgumentOutOfRangeException(parameterName, "Confidence must be between 0 and 1.");
    }
}
