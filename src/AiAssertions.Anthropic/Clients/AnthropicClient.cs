using System.Text;
using System.Text.Json.Nodes;
using AiAssertions.Anthropic.Configuration;
using AiAssertions.Core.Abstractions;
using AiAssertions.Core.Models;

namespace AiAssertions.Anthropic.Clients;

/// <summary>
/// Anthropic implementation of the AIAssert model and tool-calling client abstractions.
/// </summary>
internal sealed class AnthropicClient : IToolCallingClient
{
    private readonly HttpClient _httpClient;
    private readonly AnthropicOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnthropicClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client used to call Anthropic.</param>
    /// <param name="options">The Anthropic client options.</param>
    internal AnthropicClient(HttpClient httpClient, AnthropicOptions options)
    {
        _httpClient = httpClient;
        _options = options;

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", options.ApiKey);

        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("anthropic-version", options.ApiVersion);
    }

    /// <inheritdoc />
    public async Task<AiTextResponse> GetResponseAsync(AiTextRequest request, CancellationToken cancellationToken = default)
    {
        var body = CreateRequestBody(request.Messages, tools: null);
        var json = await SendAsync(body, cancellationToken).ConfigureAwait(false);

        return new AiTextResponse
        {
            Content = ExtractText(json)
        };
    }

    /// <inheritdoc />
    public async Task<AiToolResponse> GetToolResponseAsync(AiToolRequest request, CancellationToken cancellationToken = default)
    {
        var body = CreateRequestBody(request.Messages, CreateTools(request.Tools));
        var json = await SendAsync(body, cancellationToken).ConfigureAwait(false);
        var calls = ExtractToolCalls(json);

        return new AiToolResponse
        {
            Content = calls.Count == 0 ? ExtractText(json) : null,
            ToolCalls = calls
        };
    }

    private JsonObject CreateRequestBody(IReadOnlyList<AiChatMessage> messages, JsonArray? tools)
    {
        var body = new JsonObject
        {
            ["model"] = GetModelId(_options.Model),
            ["max_tokens"] = _options.MaxTokens,
            ["temperature"] = _options.Temperature,
            ["messages"] = CreateMessages(messages)
        };

        var system = CreateSystem(messages);
        if (!string.IsNullOrWhiteSpace(system))
            body["system"] = system;

        if (tools is not null)
        {
            body["tools"] = tools;
            body["tool_choice"] = new JsonObject
            {
                ["type"] = "auto"
            };
        }

        return body;
    }

    private async Task<JsonNode> SendAsync(JsonObject body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.MessagesPath);
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Anthropic endpoint returned {(int)response.StatusCode}: {content}");

        return JsonNode.Parse(content) ?? throw new InvalidOperationException("The model endpoint returned invalid JSON.");
    }

    private static string CreateSystem(IReadOnlyList<AiChatMessage> messages) =>
        string.Join(
            Environment.NewLine,
            messages
                .Where(message => message.Role.Equals("system", StringComparison.Ordinal))
                .Select(message => message.Content)
                .Where(content => !string.IsNullOrWhiteSpace(content)));

    private static JsonArray CreateMessages(IReadOnlyList<AiChatMessage> messages)
    {
        var array = new JsonArray();

        for (var index = 0; index < messages.Count; index++)
        {
            var message = messages[index];

            if (message.Role.Equals("system", StringComparison.Ordinal))
                continue;

            if (message.Role.Equals("tool", StringComparison.Ordinal))
            {
                var toolResults = new JsonArray();

                while (index < messages.Count && messages[index].Role.Equals("tool", StringComparison.Ordinal))
                {
                    toolResults.Add(new JsonObject
                    {
                        ["type"] = "tool_result",
                        ["tool_use_id"] = messages[index].ToolCallId,
                        ["content"] = messages[index].Content
                    });

                    index++;
                }

                index--;

                array.Add(new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = toolResults
                });

                continue;
            }

            array.Add(new JsonObject
            {
                ["role"] = message.Role.Equals("assistant", StringComparison.Ordinal) ? "assistant" : "user",
                ["content"] = CreateContent(message)
            });
        }

        return array;
    }

    private static JsonNode CreateContent(AiChatMessage message)
    {
        if (message.ToolCalls is not { Count: > 0 })
            return JsonValue.Create(message.Content);

        var content = new JsonArray();

        if (!string.IsNullOrWhiteSpace(message.Content))
            content.Add(new JsonObject
            {
                ["type"] = "text",
                ["text"] = message.Content
            });

        foreach (var call in message.ToolCalls)
            content.Add(new JsonObject
            {
                ["type"] = "tool_use",
                ["id"] = call.Id,
                ["name"] = call.Name,
                ["input"] = JsonNode.Parse(call.ArgumentsJson) ?? new JsonObject()
            });

        return content;
    }

    private static JsonArray CreateTools(IReadOnlyList<AiToolDefinition> tools)
    {
        var array = new JsonArray();

        foreach (var tool in tools)
            array.Add(new JsonObject
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["input_schema"] = JsonNode.Parse(tool.ParametersJsonSchema)
            });

        return array;
    }

    private static string ExtractText(JsonNode json)
    {
        var builder = new StringBuilder();

        if (json["content"] is JsonArray content)
            foreach (var block in content)
                if (block?["type"]?.GetValue<string>() == "text")
                    builder.Append(block["text"]?.GetValue<string>());

        return builder.ToString();
    }

    private static IReadOnlyList<AiToolCall> ExtractToolCalls(JsonNode json)
    {
        var calls = new List<AiToolCall>();

        if (json["content"] is not JsonArray content)
            return calls;

        foreach (var block in content)
        {
            if (block?["type"]?.GetValue<string>() != "tool_use")
                continue;

            var id = block["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N");
            var name = block["name"]?.GetValue<string>();
            var input = block["input"]?.ToJsonString() ?? "{}";

            if (!string.IsNullOrWhiteSpace(name))
                calls.Add(new AiToolCall
                {
                    Id = id,
                    Name = name,
                    ArgumentsJson = input
                });
        }

        return calls;
    }

    private static string GetModelId(AnthropicModel model) =>
        model switch
        {
            AnthropicModel.ClaudeSonnet45 => "claude-sonnet-4-5",
            AnthropicModel.ClaudeHaiku45 => "claude-haiku-4-5",
            AnthropicModel.ClaudeOpus48 => "claude-opus-4-8",
            _ => throw new ArgumentOutOfRangeException(nameof(model), model, "Unknown Anthropic model.")
        };
}
