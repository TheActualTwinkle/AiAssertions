# Custom Models

AiAssertions is provider-neutral. If you want to use a model that is not supported out of the box yet or use your private model, you can implement a custom provider client.

## Contracts

Custom providers implement the Core abstractions:

```csharp
using AiAssertions.Core.Abstractions;
using AiAssertions.Core.Models;

public sealed class MyModelClient : IToolCallingClient
{
    public Task<AiTextResponse> GetResponseAsync(
        AiTextRequest request,
        CancellationToken cancellationToken = default)
    {
        // Send a text request to the model and return the generated plain text response.
        throw new NotImplementedException();
    }

    public Task<AiToolResponse> GetToolResponseAsync(
        AiToolRequest request,
        CancellationToken cancellationToken = default)
    {
        // Convert AiAssertions messages and tools to your provider's request format.
        // Send the request with HttpClient, SDK, or a local model runtime.
        // Convert the provider response back to AiToolResponse.
        throw new NotImplementedException();
    }
}
```

Then configure AiAssertions with your client:

```csharp
AiAssert.Configure(new MyModelClient())
    .WithDefaultTimeout(TimeSpan.FromMinutes(2))
    .WithGlobalConfidenceTolerance(0.75);

var result = await AiAssert
    .OnCodebase()
    .That("Passwords must never be stored or logged in plain text.");
```

## Core DTOs

These DTOs are public so custom providers can translate between AiAssertions and their own model API:

- `AiChatMessage`: a chat message with role, content, optional tool call data.
- `AiTextRequest`: a text-only model request.
- `AiTextResponse`: a text-only model response.
- `AiToolRequest`: a model request that includes available tool definitions.
- `AiToolResponse`: either final assistant content or requested tool calls.
- `AiToolDefinition`: a tool name, description, and JSON schema exposed to the model.
- `AiToolCall`: a specific tool call requested by the model.

## Tool-Calling Behavior

Codebase assertions require native tool/function calling.

Your `GetToolResponseAsync` implementation must handle two response shapes:

- final content: set `AiToolResponse.Content` and return an empty `ToolCalls` list;
- tool calls: set `AiToolResponse.ToolCalls` and leave `Content` null or provider-specific.

Example final response:

```csharp
return new AiToolResponse
{
    Content = finalJson,
    ToolCalls = []
};
```

Example tool-call response:

```csharp
return new AiToolResponse
{
    Content = null,
    ToolCalls =
    [
        new AiToolCall
        {
            Id = providerToolCallId,
            Name = toolName,
            ArgumentsJson = rawArgumentsJson
        }
    ]
};
```

AiAssertions executes requested tools locally, appends tool results to the conversation, and calls your client again until the model returns final content.

The final content **must be strict JSON** with this shape:

```json
{
  "passed": true,
  "confidence": 0.92,
  "is_conclusive": true,
  "reason": "Passwords are hashed before persistence.",
  "evidence": [
    {
      "file": "SampleCode/Security/PasswordRegistrationService.cs",
      "start_line": 12,
      "end_line": 22,
      "description": "Password hash and salt are created before persistence."
    }
  ],
  "missing_evidence": []
}
```
