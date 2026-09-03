namespace AdrGuard.Generation.Providers.OpenAiCompatible;

internal sealed class OpenAiCompatibleProviderOptions
{
    internal const string ApiKeyEnvironmentVariableName =
        "ADR_GUARD_OPENAI_COMPATIBLE_API_KEY";

    internal OpenAiCompatibleProviderOptions(
        Uri baseUri,
        string model,
        string? apiKey = null)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        if (!baseUri.IsAbsoluteUri
            || baseUri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException(
                "OpenAI-compatible base endpoint must be an absolute HTTP or HTTPS URI.",
                nameof(baseUri));
        }

        if (!string.IsNullOrEmpty(baseUri.Query)
            || !string.IsNullOrEmpty(baseUri.Fragment))
        {
            throw new ArgumentException(
                "OpenAI-compatible base endpoint must not contain a query string or fragment.",
                nameof(baseUri));
        }

        var normalizedBaseUri = baseUri.AbsoluteUri.EndsWith(
            "/",
            StringComparison.Ordinal)
            ? baseUri
            : new Uri($"{baseUri.AbsoluteUri}/", UriKind.Absolute);

        BaseUri = normalizedBaseUri;
        Endpoint = new Uri(normalizedBaseUri, "chat/completions");
        Model = model.Trim();
        ApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey;
    }

    internal Uri BaseUri { get; }

    internal Uri Endpoint { get; }

    internal string Model { get; }

    internal string? ApiKey { get; }

    internal static OpenAiCompatibleProviderOptions FromEnvironment(
        Uri baseUri,
        string model,
        Func<string, string?>? environmentVariableReader = null)
    {
        var reader = environmentVariableReader
            ?? Environment.GetEnvironmentVariable;

        return new OpenAiCompatibleProviderOptions(
            baseUri,
            model,
            reader(ApiKeyEnvironmentVariableName));
    }
}
