using AiAssertions.Core.Abstractions;

namespace AiAssertions;

/// <summary>
/// Entry point for configuring AIAssert and creating codebase assertions.
/// </summary>
public static class AiAssert
{
    private static readonly Lock SyncRoot = new();
    private static IToolCallingClient? _toolCallingClient;
    private static AiAssertDefaults _defaults = new();

    /// <summary>
    /// Configures the model client used by AI-backed assertions.
    /// </summary>
    /// <param name="modelClient">The model client implementation to use.</param>
    /// <returns>A configuration builder for setting global AIAssert defaults.</returns>
    public static AiAssertConfiguration Configure(IToolCallingClient modelClient)
    {
        ArgumentNullException.ThrowIfNull(modelClient);

        lock (SyncRoot)
        {
            _toolCallingClient = modelClient;
        }

        return new AiAssertConfiguration(GetDefaults, SetDefaults);
    }

    /// <summary>
    /// Starts a codebase assertion against the current project.
    /// </summary>
    /// <returns>A fluent builder for configuring and running a codebase assertion.</returns>
    public static CodebaseAssertion OnCodebase() =>
        new(GetToolCallingClient(), GetDefaults());

    private static IToolCallingClient GetToolCallingClient()
    {
        lock (SyncRoot)
        {
            return _toolCallingClient ?? throw new InvalidOperationException("Configure AiAssert with an IToolCallingClient before using OnCodebase assertions.");
        }
    }

    private static AiAssertDefaults GetDefaults()
    {
        lock (SyncRoot)
        {
            return _defaults;
        }
    }

    private static void SetDefaults(AiAssertDefaults defaults)
    {
        lock (SyncRoot)
        {
            _defaults = defaults;
        }
    }
}
