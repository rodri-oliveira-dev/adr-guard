using AdrGuard.Generation.Http;
using AdrGuard.Generation.Providers;
using AdrGuard.Generation.Providers.Anthropic;
using AdrGuard.Generation.Providers.Gemini;
using AdrGuard.Generation.Providers.OpenAi;
using AdrGuard.Generation.Providers.OpenAiCompatible;

namespace AdrGuard.Review.Providers;

internal static class AdrReviewProviderFactory
{
    internal const int AnthropicReviewMaxTokens = 8192;
    internal static IAdrReviewProvider Create(
        string providerName,
        string model,
        string? endpoint,
        HttpClient httpClient,
        Func<string, string?>? environmentVariableReader = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentNullException.ThrowIfNull(httpClient);

        ValidateSelection(
            providerName,
            endpoint);

        var normalizedProvider =
            providerName.Trim().ToLowerInvariant();
        var transport = new AiHttpTransport(httpClient);

        return normalizedProvider switch
        {
            AdrGenerationProviderFactory.OpenAiProviderName =>
                CreateOpenAi(
                    model,
                    endpoint,
                    transport,
                    environmentVariableReader),
            AdrGenerationProviderFactory.AnthropicProviderName =>
                CreateAnthropic(
                    model,
                    endpoint,
                    transport,
                    environmentVariableReader),
            AdrGenerationProviderFactory.GeminiProviderName =>
                CreateGemini(
                    model,
                    endpoint,
                    transport,
                    environmentVariableReader),
            AdrGenerationProviderFactory.OpenAiCompatibleProviderName =>
                CreateOpenAiCompatible(
                    model,
                    endpoint,
                    transport,
                    environmentVariableReader),
            _ => throw new ArgumentException(
                $"Unsupported AI provider '{providerName}'. Supported providers: openai, anthropic, gemini, openai-compatible.",
                nameof(providerName)),
        };
    }

    internal static void ValidateSelection(
        string providerName,
        string? endpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            providerName);

        var normalizedProvider =
            providerName.Trim().ToLowerInvariant();

        switch (normalizedProvider)
        {
            case AdrGenerationProviderFactory.OpenAiProviderName:
            case AdrGenerationProviderFactory.AnthropicProviderName:
            case AdrGenerationProviderFactory.GeminiProviderName:
                RejectEndpoint(
                    normalizedProvider,
                    endpoint);
                return;

            case AdrGenerationProviderFactory.OpenAiCompatibleProviderName:
                if (string.IsNullOrWhiteSpace(endpoint))
                {
                    throw new ArgumentException(
                        "--endpoint is required when --provider openai-compatible is selected.",
                        nameof(endpoint));
                }

                if (!Uri.TryCreate(
                        endpoint,
                        UriKind.Absolute,
                        out _))
                {
                    throw new ArgumentException(
                        "OpenAI-compatible --endpoint must be an absolute HTTP or HTTPS URI.",
                        nameof(endpoint));
                }

                return;

            default:
                throw new ArgumentException(
                    $"Unsupported AI provider '{providerName}'. Supported providers: openai, anthropic, gemini, openai-compatible.",
                    nameof(providerName));
        }
    }

    private static OpenAiAdrReviewProvider CreateOpenAi(
        string model,
        string? endpoint,
        AiHttpTransport transport,
        Func<string, string?>? environmentVariableReader)
    {
        RejectEndpoint(
            AdrGenerationProviderFactory.OpenAiProviderName,
            endpoint);

        return new OpenAiAdrReviewProvider(
            transport,
            OpenAiProviderOptions.FromEnvironment(
                model,
                environmentVariableReader));
    }

    private static AnthropicAdrReviewProvider CreateAnthropic(
        string model,
        string? endpoint,
        AiHttpTransport transport,
        Func<string, string?>? environmentVariableReader)
    {
        RejectEndpoint(
            AdrGenerationProviderFactory.AnthropicProviderName,
            endpoint);

        return new AnthropicAdrReviewProvider(
            transport,
            AnthropicProviderOptions.FromEnvironment(
                model,
                maxTokens: AnthropicReviewMaxTokens,
                environmentVariableReader:
                    environmentVariableReader));
    }

    private static GeminiAdrReviewProvider CreateGemini(
        string model,
        string? endpoint,
        AiHttpTransport transport,
        Func<string, string?>? environmentVariableReader)
    {
        RejectEndpoint(
            AdrGenerationProviderFactory.GeminiProviderName,
            endpoint);

        return new GeminiAdrReviewProvider(
            transport,
            GeminiProviderOptions.FromEnvironment(
                model,
                environmentVariableReader));
    }

    private static OpenAiCompatibleAdrReviewProvider CreateOpenAiCompatible(
        string model,
        string? endpoint,
        AiHttpTransport transport,
        Func<string, string?>? environmentVariableReader)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException(
                "--endpoint is required when --provider openai-compatible is selected.",
                nameof(endpoint));
        }

        if (!Uri.TryCreate(
                endpoint,
                UriKind.Absolute,
                out var baseUri))
        {
            throw new ArgumentException(
                "OpenAI-compatible --endpoint must be an absolute HTTP or HTTPS URI.",
                nameof(endpoint));
        }

        return new OpenAiCompatibleAdrReviewProvider(
            transport,
            OpenAiCompatibleProviderOptions.FromEnvironment(
                baseUri,
                model,
                environmentVariableReader));
    }

    private static void RejectEndpoint(
        string providerName,
        string? endpoint)
    {
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException(
                $"--endpoint is only valid with --provider {AdrGenerationProviderFactory.OpenAiCompatibleProviderName}; provider '{providerName}' uses its official endpoint.",
                nameof(endpoint));
        }
    }
}
