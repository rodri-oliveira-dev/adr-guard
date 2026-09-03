using AdrGuard.Generation.Providers.Gemini;
using Xunit;

namespace AdrGuard.Tests.Generation.Providers.Gemini;

public sealed class GeminiProviderOptionsTests
{
    [Fact]
    public void ConstructorRequiresModelAndApiKey()
    {
        Assert.Throws<ArgumentException>(
            () => new GeminiProviderOptions(
                " ",
                "api-key"));

        Assert.Throws<ArgumentException>(
            () => new GeminiProviderOptions(
                "model",
                " "));
    }

    [Fact]
    public void FromEnvironmentReadsGeminiApiKey()
    {
        string? requestedVariable = null;

        var options = GeminiProviderOptions.FromEnvironment(
            "model",
            variableName =>
            {
                requestedVariable = variableName;
                return "environment-secret";
            });

        Assert.Equal(
            GeminiProviderOptions.ApiKeyEnvironmentVariableName,
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
            () => GeminiProviderOptions.FromEnvironment(
                "model",
                _ => null));

        Assert.Contains(
            GeminiProviderOptions.ApiKeyEnvironmentVariableName,
            exception.Message,
            StringComparison.Ordinal);
    }
}
