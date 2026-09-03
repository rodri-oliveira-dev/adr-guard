using AdrGuard.Generation;
using AdrGuard.Generation.Http;
using AdrGuard.Generation.Providers.Anthropic;
using System.Net;
using System.Text.Json;
using Xunit;

namespace AdrGuard.Tests.Generation.Providers.Anthropic;

public sealed class AnthropicAdrGenerationProviderTests
{
    [Fact]
    public async Task GenerateAsyncUsesMessagesApiAndStructuredOutput()
    {
        Uri? capturedUri = null;
        string? capturedApiKey = null;
        string? capturedVersion = null;
        string? capturedBody = null;

        using var client = CreateClient(
            async (request, cancellationToken) =>
            {
                capturedUri = request.RequestUri;
                capturedApiKey = GetHeader(
                    request,
                    "x-api-key");
                capturedVersion = GetHeader(
                    request,
                    "anthropic-version");
                capturedBody = await request.Content!
                    .ReadAsStringAsync(cancellationToken);

                return SuccessResponse(
                    """
                    {"context":"Context result.","decision":"Decision result.","consequences":"Consequences result."}
                    """);
            });
        var provider = CreateProvider(
            client,
            "claude-test",
            "secret-api-key",
            1536);

        var result = await provider.GenerateAsync(
            new AdrGenerationRequest(
                "Use Redis",
                "We need distributed caching.",
                "pt-BR"),
            TestContext.Current.CancellationToken);

        Assert.Equal("Context result.", result.Context);
        Assert.Equal("Decision result.", result.Decision);
        Assert.Equal(
            "Consequences result.",
            result.Consequences);

        Assert.Equal(
            AnthropicProviderOptions.Endpoint,
            capturedUri);
        Assert.Equal(
            "secret-api-key",
            capturedApiKey);
        Assert.Equal(
            AnthropicProviderOptions.ApiVersion,
            capturedVersion);
        Assert.NotNull(capturedBody);

        using var document =
            JsonDocument.Parse(capturedBody);

        var root = document.RootElement;
        Assert.Equal(
            "claude-test",
            root.GetProperty("model").GetString());
        Assert.Equal(
            1536,
            root.GetProperty("max_tokens").GetInt32());
        Assert.Contains(
            "pt-BR",
            root.GetProperty("system").GetString(),
            StringComparison.Ordinal);

        var messages =
            root.GetProperty("messages");
        Assert.Equal(1, messages.GetArrayLength());
        Assert.Equal(
            "user",
            messages[0]
                .GetProperty("role")
                .GetString());

        var userContent = messages[0]
            .GetProperty("content")
            .GetString();

        Assert.Contains(
            "Use Redis",
            userContent,
            StringComparison.Ordinal);
        Assert.Contains(
            "We need distributed caching.",
            userContent,
            StringComparison.Ordinal);

        var format = root
            .GetProperty("output_config")
            .GetProperty("format");

        Assert.Equal(
            "json_schema",
            format.GetProperty("type").GetString());

        var schema =
            format.GetProperty("schema");
        Assert.Equal(
            "object",
            schema.GetProperty("type").GetString());
        Assert.False(
            schema.GetProperty(
                "additionalProperties")
                .GetBoolean());

        var properties =
            schema.GetProperty("properties");
        Assert.Equal(
            3,
            properties.EnumerateObject().Count());
        Assert.True(
            properties.TryGetProperty(
                "context",
                out _));
        Assert.True(
            properties.TryGetProperty(
                "decision",
                out _));
        Assert.True(
            properties.TryGetProperty(
                "consequences",
                out _));

        var required = schema
            .GetProperty("required")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Equal(
            ["context", "decision", "consequences"],
            required);
    }

    [Fact]
    public async Task GenerateAsyncRejectsRefusalWithoutLeakingExplanation()
    {
        const string refusalExplanation =
            "sensitive refusal explanation";

        using var client = CreateClient(
            (_, _) => Task.FromResult(
                JsonResponse(
                    $$"""
                    {
                      "content":[],
                      "stop_reason":"end_turn",
                      "stop_details":{
                        "type":"refusal",
                        "explanation":"{{refusalExplanation}}"
                      }
                    }
                    """)));
        var provider = CreateProvider(
            client,
            "model",
            "api-key");

        var exception =
            await Assert.ThrowsAsync<AiProviderException>(
                () => provider.GenerateAsync(
                    new AdrGenerationRequest(
                        "Title",
                        "Context",
                        "en-US"),
                    TestContext.Current.CancellationToken));

        Assert.Equal(
            AiProviderErrorKind.Refused,
            exception.ErrorKind);
        Assert.DoesNotContain(
            refusalExplanation,
            exception.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{\"content\":[],\"stop_reason\":\"end_turn\"}")]
    [InlineData("{\"content\":[],\"stop_reason\":\"max_tokens\"}")]
    public async Task GenerateAsyncRejectsMissingOrIncompleteOutput(
        string responseJson)
    {
        using var client = CreateClient(
            (_, _) => Task.FromResult(
                new HttpResponseMessage(
                    HttpStatusCode.OK)
                {
                    Content =
                        new StringContent(responseJson),
                }));
        var provider = CreateProvider(
            client,
            "model",
            "api-key");

        var exception =
            await Assert.ThrowsAsync<AiProviderException>(
                () => provider.GenerateAsync(
                    new AdrGenerationRequest(
                        "Title",
                        "Context",
                        "en-US"),
                    TestContext.Current.CancellationToken));

        Assert.Equal(
            AiProviderErrorKind.InvalidResponse,
            exception.ErrorKind);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"context\":\"Context\",\"decision\":\"\",\"consequences\":\"Consequences\"}")]
    public async Task GenerateAsyncRejectsMalformedOrIncompleteAdrJson(
        string generatedContent)
    {
        using var client = CreateClient(
            (_, _) => Task.FromResult(
                SuccessResponse(generatedContent)));
        var provider = CreateProvider(
            client,
            "model",
            "api-key");

        var exception =
            await Assert.ThrowsAsync<AiProviderException>(
                () => provider.GenerateAsync(
                    new AdrGenerationRequest(
                        "Title",
                        "Context",
                        "en-US"),
                    TestContext.Current.CancellationToken));

        Assert.Equal(
            AiProviderErrorKind.InvalidResponse,
            exception.ErrorKind);
    }

    [Fact]
    public async Task GenerateAsyncPreservesTransportClassification()
    {
        using var client = CreateClient(
            (_, _) => Task.FromResult(
                new HttpResponseMessage(
                    (HttpStatusCode)429)
                {
                    Content =
                        new StringContent(
                            "sensitive provider body"),
                }));
        var provider = CreateProvider(
            client,
            "model",
            "secret-api-key");

        var exception =
            await Assert.ThrowsAsync<AiProviderException>(
                () => provider.GenerateAsync(
                    new AdrGenerationRequest(
                        "Title",
                        "Context",
                        "en-US"),
                    TestContext.Current.CancellationToken));

        Assert.Equal(
            AiProviderErrorKind.RateLimited,
            exception.ErrorKind);
        Assert.DoesNotContain(
            "secret-api-key",
            exception.Message,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "sensitive provider body",
            exception.Message,
            StringComparison.Ordinal);
    }

    private static AnthropicAdrGenerationProvider CreateProvider(
        HttpClient client,
        string model,
        string apiKey,
        int maxTokens =
            AnthropicProviderOptions.DefaultMaxTokens)
    {
        var transport = new AiHttpTransport(client);
        var options =
            new AnthropicProviderOptions(
                model,
                apiKey,
                maxTokens);

        return new AnthropicAdrGenerationProvider(
            transport,
            options);
    }

    private static string? GetHeader(
        HttpRequestMessage request,
        string name)
    {
        return request.Headers
            .TryGetValues(
                name,
                out var values)
            ? values.SingleOrDefault()
            : null;
    }

    private static HttpResponseMessage SuccessResponse(
        string generatedContent)
    {
        var escapedContent =
            JsonSerializer.Serialize(
                generatedContent);

        return JsonResponse(
            $$"""
            {
              "content":[
                {
                  "type":"text",
                  "text":{{escapedContent}}
                }
              ],
              "stop_reason":"end_turn",
              "stop_details":null
            }
            """);
    }

    private static HttpResponseMessage JsonResponse(
        string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json),
        };

    private static HttpClient CreateClient(
        Func<
            HttpRequestMessage,
            CancellationToken,
            Task<HttpResponseMessage>> handler) =>
        new(new StubHttpMessageHandler(handler));

    private sealed class StubHttpMessageHandler(
        Func<
            HttpRequestMessage,
            CancellationToken,
            Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            handler(
                request,
                cancellationToken);
    }
}
