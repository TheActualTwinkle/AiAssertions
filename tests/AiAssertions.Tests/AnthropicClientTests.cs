using System.Net;
using System.Text;
using AiAssertions.Anthropic.Clients;
using AiAssertions.Anthropic.Configuration;
using AiAssertions.Core.Models;
using FluentAssertions;
using Xunit;

namespace AiAssertions.Tests;

public sealed class AnthropicClientTests
{
    [Fact]
    public async Task GetResponseAsync_WhenPromptCachingIsReported_ShouldIncludeCachedTokensInTotals()
    {
        const string responseJson = """
                                    {
                                      "model": "claude-test",
                                      "stop_reason": "end_turn",
                                      "content": [{"type":"text","text":"ok"}],
                                      "usage": {
                                        "input_tokens": 10,
                                        "cache_creation_input_tokens": 20,
                                        "cache_read_input_tokens": 30,
                                        "output_tokens": 5
                                      }
                                    }
                                    """;
        
        using var httpClient = new HttpClient(new StaticResponseHandler(responseJson));

        httpClient.BaseAddress = new Uri("https://example.test/");

        var client = new AnthropicClient(httpClient, new AnthropicOptions
        {
            ApiKey = "test-key",
            Model = AnthropicModel.ClaudeSonnet45,
            Temperature = 0.25
        });

        var response = await client.GetResponseAsync(new AiTextRequest
        {
            Messages = [new AiChatMessage { Role = "user", Content = "hello" }]
        });

        response.Metadata.Should().NotBeNull();
        response.Metadata!.Usage.Should().BeEquivalentTo(new AiTokenUsage
        {
            PromptTokens = 60,
            CompletionTokens = 5,
            TotalTokens = 65,
            CachedTokens = 30
        });
    }

    private sealed class StaticResponseHandler(string responseJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
    }
}
