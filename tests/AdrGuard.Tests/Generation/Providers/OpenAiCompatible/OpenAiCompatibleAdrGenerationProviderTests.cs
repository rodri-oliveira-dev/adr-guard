using AdrGuard.Generation;
using AdrGuard.Generation.Http;
using AdrGuard.Generation.Providers.OpenAiCompatible;
using System.Net;
using System.Text.Json;
using Xunit;

namespace AdrGuard.Tests.Generation.Providers.OpenAiCompatible;

public sealed class OpenAiCompatibleAdrGenerationProviderTests
{
    [Fact]
    public async Task GenerateAsyncSendsCompatibleRequestAndMapsPlainJson()
    {
        Uri? capturedRequestUri = null;
        string? capturedAuthorizationScheme = null;
        string? capturedBody = null;

        using var client = CreateClient(async (request, cancellationToken) =>
        {
            capturedRequestUri = request.RequestUri;
            capturedAuthorizationScheme = request.Headers.Authorization?.Scheme;
            capturedBody = await request.Content!
                .ReadAsStringAsync(cancellationToken);

            return SuccessResponse(
                """
                {"context":"Context result.","decision":"Decision result.","consequences":"Consequences result."}
                """);
        });
        var provider = CreateProvider(client);
        var request = new AdrGenerationRequest(
            "Use Redis",
            "We need distributed caching.",
            "pt-BR");

        var result = await provider.GenerateAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal("Context result.", result.Context);
        Assert.Equal("Decision result.", result.Decision);
        Assert.Equal("Consequences result.", result.Consequences);

        Assert.Equal(
            new Uri("https://compatible.example.test/v1/chat/completions"),
            capturedRequestUri);
        Assert.Null(capturedAuthorizationScheme);
        Assert.NotNull(capturedBody);

        using var document = JsonDocument.Parse(capturedBody);
        Assert.Equal(
            "test-model",
            document.RootElement
                .GetProperty("model")
                .GetString());

        var messages = document.RootElement
            .GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());

        var systemMessage = messages[0]
            .GetProperty("content")
            .GetString();
        var userMessage = messages[1]
            .GetProperty("content")
            .GetString();

        Assert.Contains(
            "pt-BR",
            systemMessage,
            StringComparison.Ordinal);
        Assert.Contains(
            "Use Redis",
            userMessage,
            StringComparison.Ordinal);
        Assert.Contains(
            "We need distributed caching.",
            userMessage,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAsyncSendsBearerApiKeyWhenConfigured()
    {
        const string apiKey = "top-secret-key";
        string? authorizationScheme = null;
        string? authorizationParameter = null;

        using var client = CreateClient((request, _) =>
        {
            authorizationScheme = request.Headers.Authorization?.Scheme;
            authorizationParameter = request.Headers.Authorization?.Parameter;

            return Task.FromResult(
                SuccessResponse(
                    """
                    {"context":"Context.","decision":"Decision.","consequences":"Consequences."}
                    """));
        });
        var provider = CreateProvider(client, apiKey);

        await provider.GenerateAsync(
            new AdrGenerationRequest("Title", "Context", "en-US"),
            TestContext.Current.CancellationToken);

        Assert.Equal("Bearer", authorizationScheme);
        Assert.Equal(apiKey, authorizationParameter);
    }

    [Fact]
    public async Task GenerateAsyncAcceptsSingleJsonMarkdownFence()
    {
        using var client = CreateClient((_, _) =>
            Task.FromResult(
                SuccessResponse(
                    """
                    ```json
                    {"context":"Context.","decision":"Decision.","consequences":"Consequences."}
                    ```
                    """)));
        var provider = CreateProvider(client);

        var result = await provider.GenerateAsync(
            new AdrGenerationRequest("Title", "Context", "en-US"),
            TestContext.Current.CancellationToken);

        Assert.Equal("Context.", result.Context);
        Assert.Equal("Decision.", result.Decision);
        Assert.Equal("Consequences.", result.Consequences);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{\"choices\":[]}")]
    [InlineData("{\"choices\":[{\"message\":{}}]}")]
    [InlineData("{\"choices\":[{\"message\":{\"content\":\"   \"}}]}")]
    public async Task GenerateAsyncRejectsMissingAssistantContent(
        string responseJson)
    {
        using var client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson),
            }));
        var provider = CreateProvider(client);

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.GenerateAsync(
                new AdrGenerationRequest("Title", "Context", "en-US"),
                TestContext.Current.CancellationToken));

        Assert.Equal(
            AiProviderErrorKind.InvalidResponse,
            exception.ErrorKind);
    }

    [Fact]
    public async Task GenerateAsyncRejectsMalformedChatCompletionJson()
    {
        using var client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{not-json"),
            }));
        var provider = CreateProvider(client);

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.GenerateAsync(
                new AdrGenerationRequest("Title", "Context", "en-US"),
                TestContext.Current.CancellationToken));

        Assert.Equal(
            AiProviderErrorKind.InvalidResponse,
            exception.ErrorKind);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"context\":\"Context\",\"decision\":\"\",\"consequences\":\"Consequences\"}")]
    public async Task GenerateAsyncRejectsMalformedOrIncompleteGeneratedAdr(
        string generatedContent)
    {
        using var client = CreateClient((_, _) =>
            Task.FromResult(SuccessResponse(generatedContent)));
        var provider = CreateProvider(client);

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.GenerateAsync(
                new AdrGenerationRequest("Title", "Context", "en-US"),
                TestContext.Current.CancellationToken));

        Assert.Equal(
            AiProviderErrorKind.InvalidResponse,
            exception.ErrorKind);
    }

    [Fact]
    public async Task GenerateAsyncPreservesTransportErrorClassification()
    {
        using var client = CreateClient((_, _) =>
            Task.FromResult(
                new HttpResponseMessage((HttpStatusCode)429)
                {
                    Content = new StringContent("sensitive provider body"),
                }));
        var provider = CreateProvider(client, "secret-api-key");

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.GenerateAsync(
                new AdrGenerationRequest("Title", "Context", "en-US"),
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

    private static OpenAiCompatibleAdrGenerationProvider CreateProvider(
        HttpClient client,
        string? apiKey = null)
    {
        var transport = new AiHttpTransport(client);
        var options = new OpenAiCompatibleProviderOptions(
            new Uri("https://compatible.example.test/v1"),
            "test-model",
            apiKey);

        return new OpenAiCompatibleAdrGenerationProvider(
            transport,
            options);
    }

    private static HttpResponseMessage SuccessResponse(
        string generatedContent)
    {
        var responseJson = JsonSerializer.Serialize(
            new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = generatedContent,
                        },
                    },
                },
            });

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson),
        };
    }

    private static HttpClient CreateClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) =>
        new(new StubHttpMessageHandler(handler));

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }
}
