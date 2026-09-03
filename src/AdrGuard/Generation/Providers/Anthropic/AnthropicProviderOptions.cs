namespace AdrGuard.Generation.Providers.Anthropic;

internal sealed class AnthropicProviderOptions
{
    internal const string ApiKeyEnvironmentVariableName =
        "ANTHROPIC_API_KEY";

    internal const string ApiVersion = "2023-06-01";

    internal const int DefaultMaxTokens = 2048;

    internal static readonly Uri Endpoint =
        new("https://api.anthropic.com/v1/messages");

    internal AnthropicProviderOptions(
        string model,
        string apiKey,
        int maxTokens = DefaultMaxTokens)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        if (maxTokens <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxTokens),
                "Anthropic max tokens must be greater than zero.");
        }

        Model = model.Trim();
        ApiKey = apiKey;
        MaxTokens = maxTokens;
    }

    internal string Model { get; }

    internal string ApiKey { get; }

    internal int MaxTokens { get; }

    internal static AnthropicProviderOptions FromEnvironment(
        string model,
        int maxTokens = DefaultMaxTokens,
        Func<string, string?>? environmentVariableReader = null)
    {
        var reader = environmentVariableReader
            ?? Environment.GetEnvironmentVariable;
        var apiKey = reader(ApiKeyEnvironmentVariableName);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"Anthropic API key is not configured. Set the {ApiKeyEnvironmentVariableName} environment variable.");
        }

        return new AnthropicProviderOptions(
            model,
            apiKey,
            maxTokens);
    }
}
