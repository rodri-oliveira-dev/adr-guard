using AdrGuard.Generation;
using AdrGuard.Generation.Http;
using AdrGuard.Generation.Providers.Gemini;
using System.Net;
using System.Text.Json;
using Xunit;

namespace AdrGuard.Tests.Generation.Providers.Gemini;

public sealed class GeminiAdrGenerationProviderTests
{
    [Fact]
    public async Task GenerateAsyncUsesInteractionsApiAndStructuredOutput()
    {
        Uri? capturedUri = null;
        string? capturedApiKey = null;
        string? capturedBody = null;

        using var client = CreateClient(
            async (request, cancellationToken) =>
            {
                capturedUri = request.RequestUri;
                capturedApiKey = GetHeader(
                    request,
                    "x-goog-api-key");
                capturedBody = await request.Content!
                    .ReadAsStringAsync(cancellationToken);

                return SuccessResponse(
                    """
                    {"context":"Context result.","decision":"Decision result.","consequences":"Consequences result."}
                    """);
            });
        var provider = CreateProvider(
            client,
            "gemini-test",
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
            GeminiProviderOptions.Endpoint,
            capturedUri);
        Assert.Equal(
            "secret-api-key",
            capturedApiKey);
        Assert.NotNull(capturedBody);

        using var document =
            JsonDocument.Parse(capturedBody);

        var root = document.RootElement;
        Assert.Equal(
            "gemini-test",
            root.GetProperty("model").GetString());
        Assert.Contains(
            "pt-BR",
            root.GetProperty("system_instruction").GetString(),
            StringComparison.Ordinal);
        Assert.Contains(
            "Use Redis",
            root.GetProperty("input").GetString(),
            StringComparison.Ordinal);
        Assert.False(
            root.GetProperty("store").GetBoolean());

        var format =
            root.GetProperty("response_format");

        Assert.Equal(
            "text",
            format.GetProperty("type").GetString());
        Assert.Equal(
            "application/json",
            format.GetProperty("mime_type").GetString());

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
        Assert.Equal(
            3,
            properties.EnumerateObject().Count());

        var required = schema
            .GetProperty("required")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Equal(
            ["context", "decision", "consequences"],
            required);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{\"status\":\"failed\",\"steps\":[]}")]
    [InlineData("{\"status\":\"completed\",\"steps\":[]}")]
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

    private static GeminiAdrGenerationProvider CreateProvider(
        HttpClient client,
        string model,
        string apiKey)
    {
        var transport = new AiHttpTransport(client);
        var options = new GeminiProviderOptions(
            model,
            apiKey);

        return new GeminiAdrGenerationProvider(
            transport,
            options);
    }

    private static string? GetHeader(
        HttpRequestMessage request,
        string name) =>
        request.Headers.TryGetValues(
            name,
            out var values)
            ? values.SingleOrDefault()
            : null;

    private static HttpResponseMessage SuccessResponse(
        string generatedContent)
    {
        var escapedContent =
            JsonSerializer.Serialize(
                generatedContent);

        return JsonResponse(
            $$"""
            {
              "status":"completed",
              "steps":[
                {
                  "type":"model_output",
                  "content":[
                    {
                      "type":"text",
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
