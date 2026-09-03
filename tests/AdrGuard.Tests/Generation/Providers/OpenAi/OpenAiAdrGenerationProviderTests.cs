using AdrGuard.Generation;
using AdrGuard.Generation.Http;
using AdrGuard.Generation.Providers.OpenAi;
using System.Net;
using System.Text.Json;
using Xunit;

namespace AdrGuard.Tests.Generation.Providers.OpenAi;

public sealed class OpenAiAdrGenerationProviderTests
{
    [Fact]
    public async Task GenerateAsyncUsesResponsesApiStructuredOutput()
    {
        Uri? capturedUri = null;
        string? authorizationScheme = null;
        string? authorizationParameter = null;
        string? capturedBody = null;

        using var client = CreateClient(
            async (request, cancellationToken) =>
            {
                capturedUri = request.RequestUri;
                authorizationScheme =
                    request.Headers.Authorization?.Scheme;
                authorizationParameter =
                    request.Headers.Authorization?.Parameter;
                capturedBody = await request.Content!
                    .ReadAsStringAsync(cancellationToken);

                return SuccessResponse(
                    """
                    {"context":"Context result.","decision":"Decision result.","consequences":"Consequences result."}
                    """);
            });
        var provider = CreateProvider(
            client,
            "test-model",
            "secret-api-key");

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
            OpenAiProviderOptions.Endpoint,
            capturedUri);
        Assert.Equal("Bearer", authorizationScheme);
        Assert.Equal(
            "secret-api-key",
            authorizationParameter);
        Assert.NotNull(capturedBody);

        using var document =
            JsonDocument.Parse(capturedBody);

        var root = document.RootElement;
        Assert.Equal(
            "test-model",
            root.GetProperty("model").GetString());
        Assert.Contains(
            "pt-BR",
            root.GetProperty("instructions").GetString(),
            StringComparison.Ordinal);
        Assert.Contains(
            "Use Redis",
            root.GetProperty("input").GetString(),
            StringComparison.Ordinal);
        Assert.Contains(
            "We need distributed caching.",
            root.GetProperty("input").GetString(),
            StringComparison.Ordinal);

        var format = root
            .GetProperty("text")
            .GetProperty("format");

        Assert.Equal(
            "json_schema",
            format.GetProperty("type").GetString());
        Assert.Equal(
            "adr_draft",
            format.GetProperty("name").GetString());
        Assert.True(
            format.GetProperty("strict").GetBoolean());

        var schema = format.GetProperty("schema");
        Assert.Equal(
            "object",
            schema.GetProperty("type").GetString());
        Assert.False(
            schema.GetProperty(
                "additionalProperties")
                .GetBoolean());

        var properties =
            schema.GetProperty("properties");
        Assert.Equal(3, properties.EnumerateObject().Count());
        Assert.True(properties.TryGetProperty(
            "context",
            out _));
        Assert.True(properties.TryGetProperty(
            "decision",
            out _));
        Assert.True(properties.TryGetProperty(
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
    public async Task GenerateAsyncRejectsExplicitRefusalWithoutLeakingText()
    {
        const string refusalText =
            "sensitive refusal details";

        using var client = CreateClient(
            (_, _) => Task.FromResult(
                JsonResponse(
                    $$"""
                    {
                      "status":"completed",
                      "output":[
                        {
                          "type":"message",
                          "content":[
                            {
                              "type":"refusal",
                              "refusal":"{{refusalText}}"
                            }
                          ]
                        }
                      ]
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
            refusalText,
            exception.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{\"status\":\"incomplete\",\"output\":[]}")]
    [InlineData("{\"status\":\"completed\",\"output\":[]}")]
    public async Task GenerateAsyncRejectsMissingOrIncompleteOutput(
        string responseJson)
    {
        using var client = CreateClient(
            (_, _) => Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
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

    private static OpenAiAdrGenerationProvider CreateProvider(
        HttpClient client,
        string model,
        string apiKey)
    {
        var transport = new AiHttpTransport(client);
        var options =
            new OpenAiProviderOptions(
                model,
                apiKey);

        return new OpenAiAdrGenerationProvider(
            transport,
            options);
    }

    private static HttpResponseMessage SuccessResponse(
        string generatedContent)
    {
        var escapedContent =
            JsonSerializer.Serialize(generatedContent);

        return JsonResponse(
            $$"""
            {
              "status":"completed",
              "output":[
                {
                  "type":"message",
                  "content":[
                    {
                      "type":"output_text",
                      "text":{{escapedContent}}
                    }
                  ]
                }
              ]
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
