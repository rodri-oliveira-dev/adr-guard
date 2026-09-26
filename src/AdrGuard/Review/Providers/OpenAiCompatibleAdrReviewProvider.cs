using AdrGuard.Generation.Http;
using AdrGuard.Generation.Providers.OpenAiCompatible;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AdrGuard.Review.Providers;

internal sealed class OpenAiCompatibleAdrReviewProvider(
    AiHttpTransport transport,
    OpenAiCompatibleProviderOptions options) : IAdrReviewProvider
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly AiHttpTransport _transport =
        transport ?? throw new ArgumentNullException(nameof(transport));

    private readonly OpenAiCompatibleProviderOptions _options =
        options ?? throw new ArgumentNullException(nameof(options));

    public async Task<AdrReviewResult> ReviewAsync(
        AdrReviewRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var httpRequest = CreateHttpRequest(request);
        using var response = await _transport
            .SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);

        var responseJson = await response.Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        return ParseResponse(responseJson);
    }

    private HttpRequestMessage CreateHttpRequest(
        AdrReviewRequest request)
    {
        var requestBody = new ChatCompletionRequest(
            _options.Model,
            [
                new ChatMessage(
                    "system",
                    AdrReviewContract.BuildInstructions()),
                new ChatMessage(
                    "user",
                    request.ProviderContext),
            ]);

        var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            _options.Endpoint)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    requestBody,
                    JsonOptions),
                Encoding.UTF8,
                "application/json"),
        };

        if (_options.ApiKey is not null)
        {
            httpRequest.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    _options.ApiKey);
        }

        return httpRequest;
    }

    private static AdrReviewResult ParseResponse(
        string responseJson)
    {
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            throw InvalidResponse(
                "AI provider returned an empty response.");
        }

        ChatCompletionResponse? response;

        try
        {
            response = JsonSerializer.Deserialize<ChatCompletionResponse>(
                responseJson,
                JsonOptions);
        }
        catch (JsonException)
        {
            throw InvalidResponse(
                "AI provider returned malformed Chat Completions JSON.");
        }

        var content = response?
            .Choices?
            .FirstOrDefault()?
            .Message?
            .Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            throw InvalidResponse(
                "AI provider review response did not contain assistant message content.");
        }

        return AdrReviewJsonContract.Parse(
            AdrReviewJsonContract.RemoveSingleJsonFence(content),
            "OpenAI-compatible provider");
    }

    private static AiProviderException InvalidResponse(
        string message) =>
        new(
            AiProviderErrorKind.InvalidResponse,
            message);

    private sealed record ChatCompletionRequest(
        string Model,
        ChatMessage[] Messages);

    private sealed record ChatMessage(
        string Role,
        string Content);

    private sealed class ChatCompletionResponse
    {
        public ChatCompletionChoice[]? Choices { get; init; }
    }

    private sealed class ChatCompletionChoice
    {
        public ChatCompletionMessage? Message { get; init; }
    }

    private sealed class ChatCompletionMessage
    {
        public string? Content { get; init; }
    }
}
