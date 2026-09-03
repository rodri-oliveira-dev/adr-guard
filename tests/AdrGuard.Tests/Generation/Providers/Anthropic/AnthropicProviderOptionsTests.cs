using AdrGuard.Generation.Providers.Anthropic;
using Xunit;

namespace AdrGuard.Tests.Generation.Providers.Anthropic;

public sealed class AnthropicProviderOptionsTests
{
    [Fact]
    public void ConstructorRequiresModelApiKeyAndPositiveMaxTokens()
    {
        Assert.Throws<ArgumentException>(
            () => new AnthropicProviderOptions(
                " ",
                "api-key"));

        Assert.Throws<ArgumentException>(
            () => new AnthropicProviderOptions(
                "model",
                " "));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AnthropicProviderOptions(
                "model",
                "api-key",
                0));
    }

    [Fact]
    public void ConstructorUsesDefaultMaxTokens()
    {
        var options = new AnthropicProviderOptions(
            "model",
            "api-key");

        Assert.Equal(
            AnthropicProviderOptions.DefaultMaxTokens,
            options.MaxTokens);
    }

    [Fact]
    public void FromEnvironmentReadsAnthropicApiKey()
    {
        string? requestedVariable = null;

        var options = AnthropicProviderOptions.FromEnvironment(
            "model",
            environmentVariableReader: variableName =>
            {
                requestedVariable = variableName;
                return "environment-secret";
            });

        Assert.Equal(
            AnthropicProviderOptions.ApiKeyEnvironmentVariableName,
            requestedVariable);
        Assert.Equal(
            "environment-secret",
            options.ApiKey);
        Assert.Equal("model", options.Model);
    }

    [Fact]
    public void FromEnvironmentRejectsMissingApiKey()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => AnthropicProviderOptions.FromEnvironment(
                "model",
                environmentVariableReader: _ => null));

        Assert.Contains(
            AnthropicProviderOptions.ApiKeyEnvironmentVariableName,
            exception.Message,
            StringComparison.Ordinal);
    }
}
