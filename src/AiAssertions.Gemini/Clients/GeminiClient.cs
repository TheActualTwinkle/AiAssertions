using System.Text;
using System.Text.Json.Nodes;
using AiAssertions.Core.Abstractions;
using AiAssertions.Core.Models;
using AiAssertions.Gemini.Configuration;

namespace AiAssertions.Gemini.Clients;

/// <summary>
/// Gemini implementation of the AIAssert model and tool-calling client abstractions.
/// </summary>
internal sealed class GeminiClient : IToolCallingClient
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeminiClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client used to call Gemini.</param>
    /// <param name="options">The Gemini client options.</param>
    internal GeminiClient(HttpClient httpClient, GeminiOptions options)
    {
        _httpClient = httpClient;
        _options = options;
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
            ["contents"] = CreateContents(messages),
            ["generationConfig"] = new JsonObject
            {
                ["temperature"] = _options.Temperature
            }
        };

        var system = CreateSystem(messages);
        if (!string.IsNullOrWhiteSpace(system))
            body["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["text"] = system
                    }
                }
            };

        if (tools is not null)
        {
            body["tools"] = tools;
            body["toolConfig"] = new JsonObject
            {
                ["functionCallingConfig"] = new JsonObject
                {
                    ["mode"] = "AUTO"
                }
            };
        }

        return body;
    }

    private async Task<JsonNode> SendAsync(JsonObject body, CancellationToken cancellationToken)
    {
        var path = $"models/{GetModelId(_options.Model)}:generateContent?key={Uri.EscapeDataString(_options.ApiKey)}";
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Gemini endpoint returned {(int)response.StatusCode}: {content}");

        return JsonNode.Parse(content) ?? throw new InvalidOperationException("The model endpoint returned invalid JSON.");
    }

    private static string CreateSystem(IReadOnlyList<AiChatMessage> messages) =>
        string.Join(
            Environment.NewLine,
            messages
                .Where(message => message.Role.Equals("system", StringComparison.Ordinal))
                .Select(message => message.Content)
                .Where(content => !string.IsNullOrWhiteSpace(content)));

    private static JsonArray CreateContents(IReadOnlyList<AiChatMessage> messages)
    {
        var array = new JsonArray();

        for (var index = 0; index < messages.Count; index++)
        {
            var message = messages[index];

            if (message.Role.Equals("system", StringComparison.Ordinal))
                continue;

            if (message.Role.Equals("tool", StringComparison.Ordinal))
            {
                var parts = new JsonArray();

                while (index < messages.Count && messages[index].Role.Equals("tool", StringComparison.Ordinal))
                {
                    parts.Add(new JsonObject
                    {
                        ["functionResponse"] = new JsonObject
                        {
                            ["name"] = messages[index].Name,
                            ["response"] = CreateFunctionResponse(messages[index].Content)
                        }
                    });

                    index++;
                }

                index--;

                array.Add(new JsonObject
                {
                    ["role"] = "user",
                    ["parts"] = parts
                });

                continue;
            }

            array.Add(new JsonObject
            {
                ["role"] = message.Role.Equals("assistant", StringComparison.Ordinal) ? "model" : "user",
                ["parts"] = CreateParts(message)
            });
        }

        return array;
    }

    private static JsonArray CreateParts(AiChatMessage message)
    {
        var parts = new JsonArray();

        if (!string.IsNullOrWhiteSpace(message.Content))
            parts.Add(new JsonObject
            {
                ["text"] = message.Content
            });

        if (message.ToolCalls is { Count: > 0 })
            foreach (var call in message.ToolCalls)
                parts.Add(new JsonObject
                {
                    ["functionCall"] = new JsonObject
                    {
                        ["name"] = call.Name,
                        ["args"] = JsonNode.Parse(call.ArgumentsJson) ?? new JsonObject()
                    }
                });

        if (parts.Count == 0)
            parts.Add(new JsonObject
            {
                ["text"] = string.Empty
            });

        return parts;
    }

    private static JsonObject CreateFunctionResponse(string content)
    {
        try
        {
            if (JsonNode.Parse(content) is JsonObject parsed)
                return parsed;
        }
        catch
        {
            // Tool output is best-effort JSON for Gemini function responses.
        }

        return new JsonObject
        {
            ["content"] = content
        };
    }

    private static JsonArray CreateTools(IReadOnlyList<AiToolDefinition> tools)
    {
        var declarations = new JsonArray();

        foreach (var tool in tools)
            declarations.Add(new JsonObject
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["parameters"] = JsonNode.Parse(tool.ParametersJsonSchema)
            });

        return
        [
            new JsonObject
            {
                ["functionDeclarations"] = declarations
            }
        ];
    }

    private static string ExtractText(JsonNode json)
    {
        var builder = new StringBuilder();
        var parts = json["candidates"]?[0]?["content"]?["parts"] as JsonArray;

        if (parts is null)
            return string.Empty;

        foreach (var part in parts)
            builder.Append(part?["text"]?.GetValue<string>());

        return builder.ToString();
    }

    private static IReadOnlyList<AiToolCall> ExtractToolCalls(JsonNode json)
    {
        var calls = new List<AiToolCall>();
        var parts = json["candidates"]?[0]?["content"]?["parts"] as JsonArray;

        if (parts is null)
            return calls;

        foreach (var part in parts)
        {
            var call = part?["functionCall"];
            var name = call?["name"]?.GetValue<string>();

            if (!string.IsNullOrWhiteSpace(name))
                calls.Add(new AiToolCall
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = name,
                    ArgumentsJson = call?["args"]?.ToJsonString() ?? "{}"
                });
        }

        return calls;
    }

    private static string GetModelId(GeminiModel model) =>
        model switch
        {
            GeminiModel.Gemini35Flash => "gemini-3.5-flash",
            GeminiModel.Gemini35Pro => "gemini-3.5-pro",
            GeminiModel.Gemini25Flash => "gemini-2.5-flash",
            GeminiModel.Gemini25Pro => "gemini-2.5-pro",
            _ => throw new ArgumentOutOfRangeException(nameof(model), model, "Unknown Gemini model.")
        };
}
