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
    /// compaction and does not include tool definitions or provider-specific request overhead.
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
    /// <para>
    /// <c>EXPERIMENTAL</c>
    /// </para>
    /// Sets the global function used to estimate the token count of messages before each model request.
    /// </summary>
    /// <remarks>
    /// The returned estimate is compared with the limit configured by
    /// <see cref="WithGlobalApproximateTokenLimit(int)"/>.
    /// </remarks>
    /// <param name="tokenEstimator">
    /// A function that receives the selected request messages and returns their estimated token count.
    /// </param>
    /// <returns>The same configuration builder.</returns>
    public AiAssertConfiguration WithGlobalTokenEstimator(Func<IReadOnlyList<AiChatMessage>, int> tokenEstimator)
    {
        ArgumentNullException.ThrowIfNull(tokenEstimator);

        var defaults = _getDefaults();
        _setDefaults(defaults with { RequestTokenEstimator = tokenEstimator });

        return this;
    }

    /// <summary>
    /// Sets the global replacement for the codebase assertion agent's default system prompt.
    /// </summary>
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
    /// Globally disables the default summarization of older conversation history.
    /// </summary>
    /// <remarks>
    /// A global approximate token limit may still omit older messages. Calling this method after
    /// <see cref="WithGlobalConversationCompactor(Func{IReadOnlyList{AiChatMessage}, IReadOnlyList{AiChatMessage}})"/>
    /// also removes the custom compactor.
    /// </remarks>
    /// <returns>The same configuration builder.</returns>
    public AiAssertConfiguration WithoutGlobalConversationCompaction()
    {
        var defaults = _getDefaults();
        _setDefaults(defaults with
        {
            ConversationCompactionEnabled = false,
            ConversationCompactor = null
        });

        return this;
    }

    /// <summary>
    /// Sets the global function that selects the conversation messages sent with each model request.
    /// </summary>
    /// <remarks>
    /// The function receives the complete accumulated conversation. Configuring a custom compactor enables
    /// conversation compaction. A global approximate token limit is applied after this function.
    /// </remarks>
    /// <param name="conversationCompactor">
    /// A function that receives the complete conversation and returns the messages for the next model request.
    /// </param>
    /// <returns>The same configuration builder.</returns>
    public AiAssertConfiguration WithGlobalConversationCompactor(
        Func<IReadOnlyList<AiChatMessage>, IReadOnlyList<AiChatMessage>> conversationCompactor)
    {
        ArgumentNullException.ThrowIfNull(conversationCompactor);

        var defaults = _getDefaults();
        _setDefaults(defaults with
        {
            ConversationCompactionEnabled = true,
            ConversationCompactor = conversationCompactor
        });

        return this;
    }

    /// <summary>
    /// Configures the bounded default conversation compactor for subsequent assertions.
    /// </summary>
    /// <remarks>
    /// Calling this method after
    /// <see cref="WithGlobalConversationCompactor(Func{IReadOnlyList{AiChatMessage}, IReadOnlyList{AiChatMessage}})"/>
    /// removes the custom compactor. A custom compactor configured later replaces the bounded default compactor.
    /// </remarks>
    /// <param name="recentToolCallTurns">The number of newest tool-call turns retained as protocol messages.</param>
    /// <param name="maxToolResultChars">The maximum characters retained from a single tool result.</param>
    /// <param name="maxCompactedStateChars">The maximum characters retained in the structured state for older tool calls.</param>
    /// <returns>The same configuration builder.</returns>
    public AiAssertConfiguration WithGlobalConversationCompactionLimits(
        int recentToolCallTurns,
        int maxToolResultChars,
        int maxCompactedStateChars)
    {
        if (recentToolCallTurns <= 0)
            throw new ArgumentOutOfRangeException(nameof(recentToolCallTurns), "Recent tool-call turns must be positive.");
        if (maxToolResultChars <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxToolResultChars), "Maximum tool-result characters must be positive.");
        if (maxCompactedStateChars <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCompactedStateChars), "Maximum compacted-state characters must be positive.");

        var defaults = _getDefaults();
        _setDefaults(defaults with
        {
            ConversationCompactionEnabled = true,
            ConversationCompactor = null,
            RecentToolCallTurns = recentToolCallTurns,
            MaxCompactedToolResultChars = maxToolResultChars,
            MaxCompactedStateChars = maxCompactedStateChars
        });

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
