using AdrGuard.Generation.Http;
using AdrGuard.Generation.Providers.Gemini;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdrGuard.Review.Providers;

internal sealed class GeminiAdrReviewProvider(
    AiHttpTransport transport,
    GeminiProviderOptions options) : IAdrReviewProvider
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly AiHttpTransport _transport =
        transport ?? throw new ArgumentNullException(nameof(transport));

    private readonly GeminiProviderOptions _options =
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
        var requestBody = new InteractionRequest(
            _options.Model,
            request.ProviderContext,
            AdrReviewContract.BuildInstructions(),
            new TextResponseFormat(
                "text",
                "application/json",
                AdrReviewJsonContract.BuildSchema()),
            Store: false);

        var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            GeminiProviderOptions.Endpoint)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    requestBody,
                    JsonOptions),
                Encoding.UTF8,
                "application/json"),
        };

        httpRequest.Headers.TryAddWithoutValidation(
            "x-goog-api-key",
            _options.ApiKey);

        return httpRequest;
    }

    private static AdrReviewResult ParseResponse(
        string responseJson)
    {
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            throw InvalidResponse(
                "Gemini returned an empty response.");
        }

        GeminiInteractionResponse? response;

        try
        {
            response = JsonSerializer.Deserialize<GeminiInteractionResponse>(
                responseJson,
                JsonOptions);
        }
        catch (JsonException)
        {
            throw InvalidResponse(
                "Gemini returned malformed Interactions API JSON.");
        }

        if (response is null
            || !string.Equals(
                response.Status,
                "completed",
                StringComparison.Ordinal))
        {
            throw InvalidResponse(
                "Gemini review interaction did not complete successfully.");
        }

        var outputText = response.Steps?
            .Where(step => string.Equals(
                step.Type,
                "model_output",
                StringComparison.Ordinal))
            .SelectMany(step =>
                step.Content
                ?? Array.Empty<GeminiContent>())
            .FirstOrDefault(content =>
                string.Equals(
                    content.Type,
                    "text",
                    StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(content.Text))
            ?.Text;

        if (string.IsNullOrWhiteSpace(outputText))
        {
            throw InvalidResponse(
                "Gemini review interaction did not contain model output text.");
        }

        return AdrReviewJsonContract.Parse(
            outputText,
            "Gemini");
    }

    private static AiProviderException InvalidResponse(
        string message) =>
        new(
            AiProviderErrorKind.InvalidResponse,
            message);

    private sealed record InteractionRequest(
        string Model,
        string Input,
        [property: JsonPropertyName("system_instruction")]
        string SystemInstruction,
        [property: JsonPropertyName("response_format")]
        TextResponseFormat ResponseFormat,
        bool Store);

    private sealed record TextResponseFormat(
        string Type,
        [property: JsonPropertyName("mime_type")]
        string MimeType,
        AdrReviewJsonContract.ReviewJsonSchema Schema);

    private sealed class GeminiInteractionResponse
    {
        public string? Status { get; init; }

        public GeminiStep[]? Steps { get; init; }
    }

    private sealed class GeminiStep
    {
        public string? Type { get; init; }

        public GeminiContent[]? Content { get; init; }
    }

    private sealed class GeminiContent
    {
        public string? Type { get; init; }

        public string? Text { get; init; }
    }
}
