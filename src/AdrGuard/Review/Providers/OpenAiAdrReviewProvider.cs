using AdrGuard.Generation.Http;
using AdrGuard.Generation.Providers.OpenAi;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AdrGuard.Review.Providers;

internal sealed class OpenAiAdrReviewProvider(
    AiHttpTransport transport,
    OpenAiProviderOptions options) : IAdrReviewProvider
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly AiHttpTransport _transport =
        transport ?? throw new ArgumentNullException(nameof(transport));

    private readonly OpenAiProviderOptions _options =
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
        var requestBody = new ResponsesRequest(
            _options.Model,
            AdrReviewContract.BuildInstructions(),
            request.ProviderContext,
            new ResponsesText(
                new StructuredOutputFormat(
                    "json_schema",
                    "adr_review",
                    Strict: true,
                    AdrReviewJsonContract.BuildSchema())),
            Store: false);

        var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            OpenAiProviderOptions.Endpoint)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    requestBody,
                    JsonOptions),
                Encoding.UTF8,
                "application/json"),
        };

        httpRequest.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                _options.ApiKey);

        return httpRequest;
    }

    private static AdrReviewResult ParseResponse(
        string responseJson)
    {
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            throw InvalidResponse(
                "OpenAI returned an empty response.");
        }

        OpenAiResponse? response;

        try
        {
            response = JsonSerializer.Deserialize<OpenAiResponse>(
                responseJson,
                JsonOptions);
        }
        catch (JsonException)
        {
            throw InvalidResponse(
                "OpenAI returned malformed Responses API JSON.");
        }

        if (response is null
            || !string.Equals(
                response.Status,
                "completed",
                StringComparison.Ordinal))
        {
            throw InvalidResponse(
                "OpenAI review response did not complete successfully.");
        }

        var contentItems = response.Output?
            .Where(item => string.Equals(
                item.Type,
                "message",
                StringComparison.Ordinal))
            .SelectMany(item =>
                item.Content
                ?? Array.Empty<OpenAiContentItem>())
            .ToArray()
            ?? [];

        if (contentItems.Any(item => string.Equals(
                item.Type,
                "refusal",
                StringComparison.Ordinal)))
        {
            throw new AiProviderException(
                AiProviderErrorKind.Refused,
                "OpenAI refused to review the ADR.");
        }

        var outputText = contentItems
            .FirstOrDefault(item =>
                string.Equals(
                    item.Type,
                    "output_text",
                    StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(item.Text))
            ?.Text;

        if (string.IsNullOrWhiteSpace(outputText))
        {
            throw InvalidResponse(
                "OpenAI review response did not contain output text.");
        }

        return AdrReviewJsonContract.Parse(
            outputText,
            "OpenAI");
    }

    private static AiProviderException InvalidResponse(
        string message) =>
        new(
            AiProviderErrorKind.InvalidResponse,
            message);

    private sealed record ResponsesRequest(
        string Model,
        string Instructions,
        string Input,
        ResponsesText Text,
        bool Store);

    private sealed record ResponsesText(
        StructuredOutputFormat Format);

    private sealed record StructuredOutputFormat(
        string Type,
        string Name,
        bool Strict,
        AdrReviewJsonContract.ReviewJsonSchema Schema);

    private sealed class OpenAiResponse
    {
        public string? Status { get; init; }

        public OpenAiOutputItem[]? Output { get; init; }
    }

    private sealed class OpenAiOutputItem
    {
        public string? Type { get; init; }

        public OpenAiContentItem[]? Content { get; init; }
    }

    private sealed class OpenAiContentItem
    {
        public string? Type { get; init; }

        public string? Text { get; init; }

        public string? Refusal { get; init; }
    }
}
