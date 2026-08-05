using System.Collections.Concurrent;
using AiAssertions.Core.Tools.Codebase;

namespace AiAssertions.Core.Tools.Abstractions;

/// <summary>
/// Provides contextual information for local tool execution.
/// </summary>
internal sealed class ToolExecutionContext
{
    private readonly ConcurrentDictionary<string, Lazy<Task<string>>> _toolResults = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolExecutionContext"/> class.
    /// </summary>
    /// <param name="workingDirectory">The working directory used as the tool execution base.</param>
    /// <param name="codebaseRoot">The optional root used to build the shared file index.</param>
    internal ToolExecutionContext(string workingDirectory, string? codebaseRoot = null)
    {
        WorkingDirectory = Path.GetFullPath(workingDirectory);
        FileIndex = new CodebaseFileIndex(codebaseRoot ?? WorkingDirectory);
    }

    /// <summary>
    /// Gets the normalized working directory used by tools.
    /// </summary>
    internal string WorkingDirectory { get; }

    internal CodebaseFileIndex FileIndex { get; }

    internal async Task<ToolExecutionCacheResult> GetOrAddToolResultAsync(
        string key,
        Func<Task<string>> valueFactory,
        Func<string, bool>? shouldCache = null)
    {
        var candidate = new Lazy<Task<string>>(valueFactory, LazyThreadSafetyMode.ExecutionAndPublication);
        var cached = _toolResults.GetOrAdd(key, candidate);

        try
        {
            var content = await cached.Value.ConfigureAwait(false);
            var cacheable = shouldCache?.Invoke(content) ?? true;

            if (!cacheable)
                _toolResults.TryRemove(new KeyValuePair<string, Lazy<Task<string>>>(key, cached));

            return new ToolExecutionCacheResult(
                content,
                cacheable && !ReferenceEquals(candidate, cached));
        }
        catch
        {
            _toolResults.TryRemove(new KeyValuePair<string, Lazy<Task<string>>>(key, cached));
            throw;
        }
    }
}

internal readonly record struct ToolExecutionCacheResult(string Content, bool CacheHit);
