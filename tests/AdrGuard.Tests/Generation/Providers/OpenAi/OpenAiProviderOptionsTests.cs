using AdrGuard.Generation.Providers.OpenAi;
using Xunit;

namespace AdrGuard.Tests.Generation.Providers.OpenAi;

public sealed class OpenAiProviderOptionsTests
{
    [Fact]
    public void ConstructorRequiresModelAndApiKey()
    {
        Assert.Throws<ArgumentException>(
            () => new OpenAiProviderOptions(
                " ",
                "api-key"));

        Assert.Throws<ArgumentException>(
            () => new OpenAiProviderOptions(
                "model",
                " "));
    }

    [Fact]
    public void FromEnvironmentReadsOpenAiApiKey()
    {
        string? requestedVariable = null;

        var options = OpenAiProviderOptions.FromEnvironment(
            "model",
            variableName =>
            {
                requestedVariable = variableName;
                return "environment-secret";
            });

        Assert.Equal(
            OpenAiProviderOptions.ApiKeyEnvironmentVariableName,
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
            () => OpenAiProviderOptions.FromEnvironment(
                "model",
                _ => null));

        Assert.Contains(
            OpenAiProviderOptions.ApiKeyEnvironmentVariableName,
            exception.Message,
            StringComparison.Ordinal);
    }
}
