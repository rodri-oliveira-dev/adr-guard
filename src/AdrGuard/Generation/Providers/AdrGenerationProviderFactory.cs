using AdrGuard.Generation.Http;
using AdrGuard.Generation.Providers.Anthropic;
using AdrGuard.Generation.Providers.Gemini;
using AdrGuard.Generation.Providers.OpenAi;
using AdrGuard.Generation.Providers.OpenAiCompatible;

namespace AdrGuard.Generation.Providers;

internal static class AdrGenerationProviderFactory
{
    internal const string OpenAiProviderName = "openai";
    internal const string AnthropicProviderName = "anthropic";
    internal const string GeminiProviderName = "gemini";
    internal const string OpenAiCompatibleProviderName =
        "openai-compatible";

    internal static IAdrGenerationProvider Create(
        string providerName,
        string model,
        string? endpoint,
        HttpClient httpClient,
        Func<string, string?>? environmentVariableReader = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentNullException.ThrowIfNull(httpClient);

        var normalizedProvider =
            providerName.Trim().ToLowerInvariant();
        var transport = new AiHttpTransport(httpClient);

        return normalizedProvider switch
        {
            OpenAiProviderName =>
                CreateOpenAi(
                    model,
                    endpoint,
                    transport,
                    environmentVariableReader),
            AnthropicProviderName =>
                CreateAnthropic(
                    model,
                    endpoint,
                    transport,
                    environmentVariableReader),
            GeminiProviderName =>
                CreateGemini(
                    model,
                    endpoint,
                    transport,
                    environmentVariableReader),
            OpenAiCompatibleProviderName =>
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

    private static IAdrGenerationProvider CreateOpenAi(
        string model,
        string? endpoint,
        AiHttpTransport transport,
        Func<string, string?>? environmentVariableReader)
    {
        RejectEndpoint(
            OpenAiProviderName,
            endpoint);

        return new OpenAiAdrGenerationProvider(
            transport,
            OpenAiProviderOptions.FromEnvironment(
                model,
                environmentVariableReader));
    }

    private static IAdrGenerationProvider CreateAnthropic(
        string model,
        string? endpoint,
        AiHttpTransport transport,
        Func<string, string?>? environmentVariableReader)
    {
        RejectEndpoint(
            AnthropicProviderName,
            endpoint);

        return new AnthropicAdrGenerationProvider(
            transport,
            AnthropicProviderOptions.FromEnvironment(
                model,
                environmentVariableReader:
                    environmentVariableReader));
    }

    private static IAdrGenerationProvider CreateGemini(
        string model,
        string? endpoint,
        AiHttpTransport transport,
        Func<string, string?>? environmentVariableReader)
    {
        RejectEndpoint(
            GeminiProviderName,
            endpoint);

        return new GeminiAdrGenerationProvider(
            transport,
            GeminiProviderOptions.FromEnvironment(
                model,
                environmentVariableReader));
    }

    private static IAdrGenerationProvider CreateOpenAiCompatible(
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

        return new OpenAiCompatibleAdrGenerationProvider(
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
                $"--endpoint is only valid with --provider {OpenAiCompatibleProviderName}; provider '{providerName}' uses its official endpoint.",
                nameof(endpoint));
        }
    }
}
