namespace AdrGuard.Generation.Providers.Gemini;

internal sealed class GeminiProviderOptions
{
    internal const string ApiKeyEnvironmentVariableName =
        "GEMINI_API_KEY";

    internal static readonly Uri Endpoint =
        new("https://generativelanguage.googleapis.com/v1beta/interactions");

    internal GeminiProviderOptions(
        string model,
        string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        Model = model.Trim();
        ApiKey = apiKey;
    }

    internal string Model { get; }

    internal string ApiKey { get; }

    internal static GeminiProviderOptions FromEnvironment(
        string model,
        Func<string, string?>? environmentVariableReader = null)
    {
        var reader = environmentVariableReader
            ?? Environment.GetEnvironmentVariable;
        var apiKey = reader(ApiKeyEnvironmentVariableName);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"Gemini API key is not configured. Set the {ApiKeyEnvironmentVariableName} environment variable.");
        }

        return new GeminiProviderOptions(
            model,
            apiKey);
    }
}
