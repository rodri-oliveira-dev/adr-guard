using AdrGuard.Generation.Http;
using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace AdrGuard.Tests.Generation.Http;

public sealed class AiHttpTransportTests
{
    [Fact]
    public async Task SendAsyncReturnsSuccessfulResponse()
    {
        using var client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}"),
            }));
        var transport = new AiHttpTransport(client);
        using var request = CreateRequest();

        using var response = await transport.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "{\"ok\":true}",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task SendAsyncClassifiesAuthenticationFailures(
        HttpStatusCode statusCode)
    {
        using var client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(statusCode)));
        var transport = new AiHttpTransport(client);
        using var request = CreateRequest();

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => transport.SendAsync(request, TestContext.Current.CancellationToken));

        Assert.Equal(AiProviderErrorKind.Authentication, exception.ErrorKind);
        Assert.Equal(statusCode, exception.StatusCode);
    }

    [Fact]
    public async Task SendAsyncClassifiesRateLimit()
    {
        using var client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(
                (HttpStatusCode)429)));
        var transport = new AiHttpTransport(client);
        using var request = CreateRequest();

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => transport.SendAsync(request, TestContext.Current.CancellationToken));

        Assert.Equal(AiProviderErrorKind.RateLimited, exception.ErrorKind);
        Assert.Equal((HttpStatusCode)429, exception.StatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task SendAsyncClassifiesServerFailures(
        HttpStatusCode statusCode)
    {
        using var client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(statusCode)));
        var transport = new AiHttpTransport(client);
        using var request = CreateRequest();

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => transport.SendAsync(request, TestContext.Current.CancellationToken));

        Assert.Equal(AiProviderErrorKind.ServiceUnavailable, exception.ErrorKind);
        Assert.Equal(statusCode, exception.StatusCode);
    }

    [Fact]
    public async Task SendAsyncClassifiesOtherNonSuccessStatus()
    {
        using var client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)));
        var transport = new AiHttpTransport(client);
        using var request = CreateRequest();

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => transport.SendAsync(request, TestContext.Current.CancellationToken));

        Assert.Equal(AiProviderErrorKind.UnexpectedResponse, exception.ErrorKind);
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
    }

    [Fact]
    public async Task SendAsyncClassifiesTimeout()
    {
        using var client = CreateClient(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var transport = new AiHttpTransport(
            client,
            TimeSpan.FromMilliseconds(20));
        using var request = CreateRequest();

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => transport.SendAsync(request, TestContext.Current.CancellationToken));

        Assert.Equal(AiProviderErrorKind.Timeout, exception.ErrorKind);
        Assert.Null(exception.StatusCode);
    }

    [Fact]
    public async Task SendAsyncPreservesCallerCancellation()
    {
        using var client = CreateClient(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var transport = new AiHttpTransport(client, TimeSpan.FromSeconds(5));
        using var request = CreateRequest();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => transport.SendAsync(request, cancellationSource.Token));

        Assert.IsNotType<AiProviderException>(exception);
    }

    [Fact]
    public async Task SendAsyncClassifiesNetworkFailureWithoutLeakingDetails()
    {
        const string sensitiveDetail = "secret-network-detail";

        using var client = CreateClient((_, _) =>
            Task.FromException<HttpResponseMessage>(
                new HttpRequestException(sensitiveDetail)));
        var transport = new AiHttpTransport(client);
        using var request = CreateRequest();

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => transport.SendAsync(request, TestContext.Current.CancellationToken));

        Assert.Equal(AiProviderErrorKind.Network, exception.ErrorKind);
        Assert.DoesNotContain(
            sensitiveDetail,
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsyncDoesNotExposeAuthorizationOrResponseBody()
    {
        const string apiKey = "super-secret-api-key";
        const string providerBody = "provider-body-with-secret-details";

        using var client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(providerBody),
            }));
        var transport = new AiHttpTransport(client);
        using var request = CreateRequest();
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => transport.SendAsync(request, TestContext.Current.CancellationToken));

        Assert.DoesNotContain(
            apiKey,
            exception.Message,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            providerBody,
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ConstructorRejectsNonPositiveOrInfiniteTimeout()
    {
        using var client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AiHttpTransport(client, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AiHttpTransport(client, Timeout.InfiniteTimeSpan));
    }

    private static HttpClient CreateClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) =>
        new(new StubHttpMessageHandler(handler));

    private static HttpRequestMessage CreateRequest() =>
        new(HttpMethod.Post, "https://provider.example.test/v1/generate");

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
