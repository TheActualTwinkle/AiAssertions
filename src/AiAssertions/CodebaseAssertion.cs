using AiAssertions.Core.Abstractions;
using AiAssertions.Core.Agent;
using AiAssertions.Core.Assertions;

namespace AiAssertions;

/// <summary>
/// Fluent builder for configuring and executing an AI-powered codebase assertion.
/// </summary>
public sealed class CodebaseAssertion
{
    private readonly IToolCallingClient _client;
    private readonly IReadOnlyList<string> _includedPaths;
    private readonly IReadOnlyList<string> _includedTypes;
    private readonly int _maxToolIterations;
    private readonly double _minimumFalseConfidence;
    private readonly double _minimumTrueConfidence;
    private readonly TimeSpan _timeout;

    internal CodebaseAssertion(IToolCallingClient client, AiAssertDefaults defaults)
        : this(
            client,
            [],
            [],
            defaults.Timeout,
            defaults.MaxToolIterations,
            defaults.MinimumTrueConfidence,
            defaults.MinimumFalseConfidence)
    {
    }

    private CodebaseAssertion(
        IToolCallingClient client,
        IReadOnlyList<string> includedPaths,
        IReadOnlyList<string> includedTypes,
        TimeSpan timeout,
        int maxToolIterations,
        double minimumTrueConfidence,
        double minimumFalseConfidence)
    {
        _client = client;
        _includedPaths = includedPaths;
        _includedTypes = includedTypes;
        _timeout = timeout;
        _maxToolIterations = maxToolIterations;
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
            Timeout = timeout,
            MaxToolIterations = _maxToolIterations,
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
        TimeSpan? timeout = null,
        int? maxToolIterations = null,
        double? minimumTrueConfidence = null,
        double? minimumFalseConfidence = null) =>
        new(
            _client,
            includedPaths ?? _includedPaths,
            includedTypes ?? _includedTypes,
            timeout ?? _timeout,
            maxToolIterations ?? _maxToolIterations,
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
