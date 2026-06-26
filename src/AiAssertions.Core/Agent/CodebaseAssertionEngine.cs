using System.Text;
using System.Text.Json;
using AiAssertions.Core.Abstractions;
using AiAssertions.Core.Assertions;
using AiAssertions.Core.Models;
using AiAssertions.Core.Tools.Abstractions;
using AiAssertions.Core.Tools.Codebase;

namespace AiAssertions.Core.Agent;

internal sealed class CodebaseAssertionEngine
{
    private const string SystemPrompt = """
                                        You are AiAssert, a strict codebase assertion agent.
                                        Decide whether the requirement is satisfied by gathering evidence with tools.
                                        You cannot access the filesystem directly. Use only the provided tools.
                                        Do not guess when evidence can be gathered.
                                        When you ready to return a verdict, do not call any more tools and return the JSON in a single code block.
                                        Return the final verdict as strict JSON only:
                                        {"passed":true|false,"confidence":0.0-1.0,"is_conclusive":true|false,"reason":"...","evidence":[{"file":"...","start_line":1,"end_line":3,"description":"..."}],"missing_evidence":[{"description":"...","expected_location":"..."}]}
                                        If you cannot find enough relevant code or evidence, return "is_conclusive": false, "passed": false, and explain what is missing.
                                        Most important rule:
                                        Never return any other text outside the JSON code block. Do not include any additional commentary or explanations.
                                        "reason" must be a concise summary of the evidence and reasoning behind the verdict with max 150 characters. 
                                        "evidence" must contain only concrete code evidence with exact file paths (relative to project root) and one-based line ranges.
                                        "missing_evidence" must describe relevant evidence that was expected or needed but not found.
                                        If any of this rules are violated, the verdict will be considered invalid and the assertion will fail.

                                        THIS IS AN EXAMPLE OF A GOOD VERDICT:
                                        ```json
                                        {"passed":true,"confidence":1.0,"is_conclusive":true,"reason":"Password is hashed with salt before storage; no plain text stored or logged.","evidence":[{"file":"SampleCode/Security/PasswordRegistrationService.cs","start_line":12,"end_line":22,"description":"Password hash and salt are created before user registration."},{"file":"SampleCode/Security/RegisteredUser.cs","start_line":3,"end_line":8,"description":"Registered user stores only password hash and salt."}],"missing_evidence":[]}
                                        ```
                                        """;

    private readonly IToolCallingClient _client;
    private readonly CodebaseAssertionOptions _options;
    private readonly IReadOnlyList<IAiTool> _tools;

    internal CodebaseAssertionEngine(
        IToolCallingClient client,
        IEnumerable<IAiTool>? tools = null,
        CodebaseAssertionOptions? options = null)
    {
        _client = client;
        _tools = (tools ?? DefaultCodebaseTools.Create()).ToArray();
        _options = options ?? new CodebaseAssertionOptions();
    }

    internal async Task<AiAssertionResult> EvaluateAsync(string requirement, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requirement);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        if (_options.Timeout > TimeSpan.Zero)
            timeoutCts.CancelAfter(_options.Timeout);

        try
        {
            return await EvaluateCoreAsync(requirement, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new AiAssertionResult
            {
                Passed = false,
                Confidence = 0,
                Reason = $"Timed out after {_options.Timeout}.",
                Evidence = [],
                MissingEvidence = [],
                IsConclusive = false
            };
        }
    }

    private async Task<AiAssertionResult> EvaluateCoreAsync(string requirement, CancellationToken cancellationToken)
    {
        var workingDirectory = _options.WorkingDirectory ?? Directory.GetCurrentDirectory();
        var context = new ToolExecutionContext(workingDirectory);
        var userMessage = await BuildUserMessageAsync(requirement, workingDirectory, cancellationToken).ConfigureAwait(false);
        
        var messages = new List<AiChatMessage>
        {
            new()
            {
                Role = "system",
                Content = SystemPrompt
            },
            new()
            {
                Role = "user",
                Content = userMessage
            }
        };

        var toolDefinitions = _tools
            .Select(tool => new AiToolDefinition
            {
                Name = tool.Name,
                Description = tool.Description,
                ParametersJsonSchema = tool.ParametersJsonSchema
            })
            .ToArray();

        for (var step = 0; step < _options.MaxToolIterations; step++)
        {
            var response = await _client
                .GetToolResponseAsync(
                    new AiToolRequest
                    {
                        Messages = messages,
                        Tools = toolDefinitions
                    }, cancellationToken)
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
                Content = string.Empty,
                ToolCalls = response.ToolCalls
            });

            foreach (var call in response.ToolCalls)
            {
                var tool = _tools.FirstOrDefault(candidate => candidate.Name.Equals(call.Name, StringComparison.Ordinal));
                
                var content = tool is null
                    ? JsonSerializer.Serialize(new { error = $"Unknown tool '{call.Name}'." }, AssertionJson.Options)
                    : await ExecuteToolAsync(tool, call.ArgumentsJson, context, cancellationToken).ConfigureAwait(false);
                
                messages.Add(new AiChatMessage
                {
                    Role = "tool",
                    Content = content,
                    Name = call.Name,
                    ToolCallId = call.Id
                });
            }
        }

        var transcript = new StringBuilder();
        
        foreach (var message in messages.TakeLast(8))
        {
            transcript.AppendLine(message.Role);
            transcript.AppendLine(message.Content);
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
                    ExpectedLocation = transcript.ToString()
                }
            ],
            IsConclusive = false
        };
    }

    private async Task<string> BuildUserMessageAsync(string requirement, string workingDirectory, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        
        builder.AppendLine("Requirement:");
        builder.AppendLine(requirement.Trim());

        var includedEvidence = await ReadIncludedEvidenceAsync(workingDirectory, cancellationToken).ConfigureAwait(false);
        
        if (includedEvidence.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Pre-included code evidence:");
            builder.Append(includedEvidence);
        }

        return builder.ToString();
    }

    private async Task<string> ReadIncludedEvidenceAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        var files = ResolveIncludedFiles(workingDirectory).Take(20).ToArray();
        
        if (files.Length == 0)
            return string.Empty;

        var root = Path.GetFullPath(workingDirectory);
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
            return await tool.ExecuteAsync(argumentsJson, context, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, AssertionJson.Options);
        }
    }
}
