using System.Text.Json;
using AiAssertions.Core.Assertions;
using AiAssertions.Core.Tools.Abstractions;

namespace AiAssertions.Core.Tools.Codebase;

internal abstract class JsonTool<TArguments> : IAiTool
    where TArguments : class, new()
{
    public abstract string Name { get; }

    public abstract string Description { get; }

    public abstract string ParametersJsonSchema { get; }

    public async ValueTask<string> ExecuteAsync(string argumentsJson, ToolExecutionContext context, CancellationToken cancellationToken = default)
    {
        var arguments = string.IsNullOrWhiteSpace(argumentsJson)
            ? new TArguments()
            : JsonSerializer.Deserialize<TArguments>(argumentsJson, AssertionJson.Options) ?? new TArguments();

        var result = await ExecuteAsync(arguments, context, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Serialize(result, AssertionJson.Options);
    }

    protected abstract ValueTask<object> ExecuteAsync(TArguments arguments, ToolExecutionContext context, CancellationToken cancellationToken);
}
