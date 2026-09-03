using System.Net;

namespace AdrGuard.Generation.Http;

internal sealed class AiHttpTransport
{
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(100);

    private readonly HttpClient _httpClient;
    private readonly TimeSpan _timeout;

    internal AiHttpTransport(
        HttpClient httpClient,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        var effectiveTimeout = timeout ?? DefaultTimeout;
        if (effectiveTimeout <= TimeSpan.Zero
            || effectiveTimeout == Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                "AI provider HTTP timeout must be a positive, finite duration.");
        }

        _httpClient = httpClient;
        _timeout = effectiveTimeout;
    }

    internal async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeoutSource.CancelAfter(_timeout);

        try
        {
            var response = await _httpClient
                .SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutSource.Token)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            var exception = CreateHttpFailure(response.StatusCode);
            response.Dispose();
            throw exception;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw new AiProviderException(
                AiProviderErrorKind.Timeout,
                "AI provider request timed out.");
        }
        catch (HttpRequestException)
        {
            throw new AiProviderException(
                AiProviderErrorKind.Network,
                "AI provider request failed due to a network or transport error.");
        }
    }

    private static AiProviderException CreateHttpFailure(HttpStatusCode statusCode)
    {
        var numericStatusCode = (int)statusCode;

        if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return new AiProviderException(
                AiProviderErrorKind.Authentication,
                $"AI provider rejected authentication or authorization (HTTP {numericStatusCode}).",
                statusCode);
        }

        if (numericStatusCode == 429)
        {
            return new AiProviderException(
                AiProviderErrorKind.RateLimited,
                "AI provider rate limit exceeded (HTTP 429).",
                statusCode);
        }

        if (numericStatusCode is >= 500 and <= 599)
        {
            return new AiProviderException(
                AiProviderErrorKind.ServiceUnavailable,
                $"AI provider service is unavailable (HTTP {numericStatusCode}).",
                statusCode);
        }

        return new AiProviderException(
            AiProviderErrorKind.UnexpectedResponse,
            $"AI provider returned an unexpected HTTP status (HTTP {numericStatusCode}).",
            statusCode);
    }
}
