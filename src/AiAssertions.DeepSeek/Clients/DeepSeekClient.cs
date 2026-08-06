using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using AiAssertions.Core.Abstractions;
using AiAssertions.Core.Models;
using AiAssertions.DeepSeek.Configuration;

namespace AiAssertions.DeepSeek.Clients;

/// <summary>
/// DeepSeek implementation of the AIAssert model and tool-calling client abstractions.
/// </summary>
internal sealed class DeepSeekClient : IToolCallingClient
{
    private readonly HttpClient _httpClient;
    private readonly DeepSeekOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeepSeekClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client used to call DeepSeek.</param>
    /// <param name="options">The DeepSeek client options.</param>
    internal DeepSeekClient(HttpClient httpClient, DeepSeekOptions options)
    {
        _httpClient = httpClient;
        _options = options;

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
    }

    /// <inheritdoc />
    public AiModelRequestMetadata RequestMetadata => new()
    {
        Provider = "DeepSeek",
        RequestedModel = GetModelId(_options.Model),
        Temperature = _options.Temperature
    };

    /// <inheritdoc />
    public async Task<AiTextResponse> GetResponseAsync(AiTextRequest request, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject
        {
            ["model"] = GetModelId(_options.Model),
            ["temperature"] = _options.Temperature,
            ["messages"] = CreateMessages(request.Messages),
            ["stream"] = false
        };

        var json = await SendAsync(body, cancellationToken).ConfigureAwait(false);
        var content = json["choices"]?[0]?["message"]?["content"]?.GetValue<string>() ?? string.Empty;
        
        return new AiTextResponse
        {
            Content = content,
            Metadata = CreateResponseMetadata(json)
        };
    }

    /// <inheritdoc />
    public async Task<AiToolResponse> GetToolResponseAsync(AiToolRequest request, CancellationToken cancellationToken = default)
    {
        var tools = new JsonArray();
        
        foreach (var tool in request.Tools)
            tools.Add(new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = JsonNode.Parse(tool.ParametersJsonSchema)
                }
            });

        var body = new JsonObject
        {
            ["model"] = GetModelId(_options.Model),
            ["temperature"] = _options.Temperature,
            ["messages"] = CreateMessages(request.Messages),
            ["tools"] = tools,
            ["tool_choice"] = "auto",
            ["stream"] = false
        };

        var json = await SendAsync(body, cancellationToken).ConfigureAwait(false);
        var message = json["choices"]?[0]?["message"];
        var content = message?["content"]?.GetValue<string>();
        var calls = new List<AiToolCall>();

        if (message?["tool_calls"] is JsonArray toolCalls)
            foreach (var call in toolCalls)
            {
                var id = call?["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N");
                var function = call?["function"];
                var name = function?["name"]?.GetValue<string>();
                var arguments = function?["arguments"]?.GetValue<string>() ?? "{}";
                if (!string.IsNullOrWhiteSpace(name))
                    calls.Add(new AiToolCall
                    {
                        Id = id,
                        Name = name,
                        ArgumentsJson = arguments
                    });
            }

        return new AiToolResponse
        {
            Content = content,
            ToolCalls = calls,
            Metadata = CreateResponseMetadata(json)
        };
    }

    private AiModelResponseMetadata CreateResponseMetadata(JsonNode json)
    {
        var requestMetadata = RequestMetadata;
        var usage = json["usage"];

        return new AiModelResponseMetadata
        {
            Provider = requestMetadata.Provider,
            RequestedModel = requestMetadata.RequestedModel,
            ResponseModel = json["model"]?.GetValue<string>(),
            Temperature = requestMetadata.Temperature,
            FinishReason = json["choices"]?[0]?["finish_reason"]?.GetValue<string>(),
            Usage = usage is null
                ? null
                : new AiTokenUsage
                {
                    PromptTokens = GetTokenCount(usage["prompt_tokens"]),
                    CompletionTokens = GetTokenCount(usage["completion_tokens"]),
                    TotalTokens = GetTokenCount(usage["total_tokens"]),
                    CachedTokens = GetTokenCount(usage["prompt_cache_hit_tokens"])
                        ?? GetTokenCount(usage["prompt_tokens_details"]?["cached_tokens"]),
                    ReasoningTokens = GetTokenCount(usage["completion_tokens_details"]?["reasoning_tokens"])
                }
        };
    }

    private static long? GetTokenCount(JsonNode? value) =>
        value?.GetValue<long>();

    private async Task<JsonNode> SendAsync(JsonObject body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.ChatCompletionsPath);

        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"DeepSeek endpoint returned {(int)response.StatusCode}: {content}");

        return JsonNode.Parse(content) ?? throw new InvalidOperationException("The model endpoint returned invalid JSON.");
    }

    private static JsonArray CreateMessages(IReadOnlyList<AiChatMessage> messages)
    {
        var array = new JsonArray();
        foreach (var message in messages)
        {
            var item = new JsonObject
            {
                ["role"] = message.Role
            };

            if (message.Role == "assistant" && message.ToolCalls is { Count: > 0 } && string.IsNullOrEmpty(message.Content))
                item["content"] = null;
            else
                item["content"] = message.Content;

            if (!string.IsNullOrWhiteSpace(message.Name) && message.Role != "tool")
                item["name"] = message.Name;

            if (!string.IsNullOrWhiteSpace(message.ToolCallId))
                item["tool_call_id"] = message.ToolCallId;

            if (message.ToolCalls is { Count: > 0 })
            {
                var calls = new JsonArray();
                foreach (var call in message.ToolCalls)
                    calls.Add(new JsonObject
                    {
                        ["id"] = call.Id,
                        ["type"] = "function",
                        ["function"] = new JsonObject
                        {
                            ["name"] = call.Name,
                            ["arguments"] = call.ArgumentsJson
                        }
                    });

                item["tool_calls"] = calls;
            }

            array.Add(item);
        }

        return array;
    }

    private static string GetModelId(DeepSeekModel model) =>
        model switch
        {
            DeepSeekModel.Chat => "deepseek-chat",
            DeepSeekModel.Reasoner => "deepseek-reasoner",
            DeepSeekModel.V4Pro => "deepseek-v4-pro",
            _ => throw new ArgumentOutOfRangeException(nameof(model), model, "Unknown DeepSeek model.")
        };
}
