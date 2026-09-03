namespace AdrGuard.Generation.Providers.OpenAi;

internal sealed class OpenAiProviderOptions
{
    internal const string ApiKeyEnvironmentVariableName = "OPENAI_API_KEY";

    internal static readonly Uri Endpoint =
        new("https://api.openai.com/v1/responses");

    internal OpenAiProviderOptions(
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

    internal static OpenAiProviderOptions FromEnvironment(
        string model,
        Func<string, string?>? environmentVariableReader = null)
    {
        var reader = environmentVariableReader
            ?? Environment.GetEnvironmentVariable;
        var apiKey = reader(ApiKeyEnvironmentVariableName);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"OpenAI API key is not configured. Set the {ApiKeyEnvironmentVariableName} environment variable.");
        }

        return new OpenAiProviderOptions(
            model,
            apiKey);
    }
}
