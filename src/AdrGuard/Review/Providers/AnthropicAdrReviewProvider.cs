using AdrGuard.Generation.Http;
using AdrGuard.Generation.Providers.Anthropic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdrGuard.Review.Providers;

internal sealed class AnthropicAdrReviewProvider(
    AiHttpTransport transport,
    AnthropicProviderOptions options) : IAdrReviewProvider
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly AiHttpTransport _transport =
        transport ?? throw new ArgumentNullException(nameof(transport));

    private readonly AnthropicProviderOptions _options =
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
        var requestBody = new MessagesRequest(
            _options.Model,
            _options.MaxTokens,
            AdrReviewContract.BuildInstructions(),
            [new Message("user", request.ProviderContext)],
            new OutputConfig(
                new JsonOutputFormat(
                    "json_schema",
                    AdrReviewJsonContract.BuildSchema())));

        var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            AnthropicProviderOptions.Endpoint)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    requestBody,
                    JsonOptions),
                Encoding.UTF8,
                "application/json"),
        };

        httpRequest.Headers.TryAddWithoutValidation(
            "x-api-key",
            _options.ApiKey);
        httpRequest.Headers.TryAddWithoutValidation(
            "anthropic-version",
            AnthropicProviderOptions.ApiVersion);

        return httpRequest;
    }

    private static AdrReviewResult ParseResponse(
        string responseJson)
    {
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            throw InvalidResponse(
                "Anthropic returned an empty response.");
        }

        AnthropicResponse? response;

        try
        {
            response = JsonSerializer.Deserialize<AnthropicResponse>(
                responseJson,
                JsonOptions);
        }
        catch (JsonException)
        {
            throw InvalidResponse(
                "Anthropic returned malformed Messages API JSON.");
        }

        if (response is null)
        {
            throw InvalidResponse(
                "Anthropic returned an empty Messages API payload.");
        }

        if (string.Equals(
                response.StopDetails?.Type,
                "refusal",
                StringComparison.Ordinal))
        {
            throw new AiProviderException(
                AiProviderErrorKind.Refused,
                "Anthropic refused to review the ADR.");
        }

        if (!string.Equals(
                response.StopReason,
                "end_turn",
                StringComparison.Ordinal))
        {
            throw InvalidResponse(
                "Anthropic review response did not complete normally.");
        }

        var outputText = response.Content?
            .FirstOrDefault(item =>
                string.Equals(
                    item.Type,
                    "text",
                    StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(item.Text))
            ?.Text;

        if (string.IsNullOrWhiteSpace(outputText))
        {
            throw InvalidResponse(
                "Anthropic review response did not contain text content.");
        }

        return AdrReviewJsonContract.Parse(
            outputText,
            "Anthropic");
    }

    private static AiProviderException InvalidResponse(
        string message) =>
        new(
            AiProviderErrorKind.InvalidResponse,
            message);

    private sealed record MessagesRequest(
        string Model,
        [property: JsonPropertyName("max_tokens")]
        int MaxTokens,
        string System,
        Message[] Messages,
        [property: JsonPropertyName("output_config")]
        OutputConfig OutputConfig);

    private sealed record Message(
        string Role,
        string Content);

    private sealed record OutputConfig(
        JsonOutputFormat Format);

    private sealed record JsonOutputFormat(
        string Type,
        AdrReviewJsonContract.ReviewJsonSchema Schema);

    private sealed class AnthropicResponse
    {
        public AnthropicContentBlock[]? Content { get; init; }

        [JsonPropertyName("stop_reason")]
        public string? StopReason { get; init; }

        [JsonPropertyName("stop_details")]
        public AnthropicStopDetails? StopDetails { get; init; }
    }

    private sealed class AnthropicContentBlock
    {
        public string? Type { get; init; }

        public string? Text { get; init; }
    }

    private sealed class AnthropicStopDetails
    {
        public string? Type { get; init; }
    }
}
