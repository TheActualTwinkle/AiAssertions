using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using AiAssertions.Core.Abstractions;
using AiAssertions.Core.Models;
using AiAssertions.OpenRouter.Configuration;

namespace AiAssertions.OpenRouter.Clients;

/// <summary>
/// OpenRouter implementation of the AIAssert model and tool-calling client abstractions.
/// </summary>
internal sealed class OpenRouterClient : IToolCallingClient
{
    private readonly HttpClient _httpClient;
    private readonly OpenRouterOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenRouterClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client used to call OpenRouter.</param>
    /// <param name="options">The OpenRouter client options.</param>
    internal OpenRouterClient(HttpClient httpClient, OpenRouterOptions options)
    {
        _httpClient = httpClient;
        _options = options;

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        if (!string.IsNullOrWhiteSpace(options.HttpReferer))
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("HTTP-Referer", options.HttpReferer);

        if (!string.IsNullOrWhiteSpace(options.Title))
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Title", options.Title);
    }

    /// <inheritdoc />
    public async Task<AiTextResponse> GetResponseAsync(AiTextRequest request, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject
        {
            ["model"] = GetModelId(_options.Model),
            ["temperature"] = _options.Temperature,
            ["messages"] = CreateMessages(request.Messages)
        };

        var json = await SendAsync(body, cancellationToken).ConfigureAwait(false);
        var content = json["choices"]?[0]?["message"]?["content"]?.GetValue<string>() ?? string.Empty;
        
        return new AiTextResponse
        {
            Content = content
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
            ["tool_choice"] = "auto"
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
            ToolCalls = calls
        };
    }

    private async Task<JsonNode> SendAsync(JsonObject body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");

        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"OpenRouter endpoint returned {(int)response.StatusCode}: {content}");

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

    private static string GetModelId(OpenRouterModel model) =>
        model switch
        {
            OpenRouterModel.OpenAiGpt4O => "openai/gpt-4o",
            OpenRouterModel.OpenAiGpt4OMini => "openai/gpt-4o-mini",
            OpenRouterModel.OpenAiGpt55 => "openai/gpt-5.5",
            OpenRouterModel.OpenAiGpt54 => "openai/gpt-5.4",
            OpenRouterModel.DeepSeekChat => "deepseek/deepseek-chat",
            OpenRouterModel.DeepSeekReasoner => "deepseek/deepseek-reasoner",
            OpenRouterModel.DeepSeekV4Pro => "deepseek/deepseek-v4-pro",
            _ => throw new ArgumentOutOfRangeException(nameof(model), model, "Unknown OpenRouter model.")
        };
}
