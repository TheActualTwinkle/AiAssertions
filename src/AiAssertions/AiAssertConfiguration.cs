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
