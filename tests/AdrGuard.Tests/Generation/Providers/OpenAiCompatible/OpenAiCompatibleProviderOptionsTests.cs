using AdrGuard.Generation.Providers.OpenAiCompatible;
using Xunit;

namespace AdrGuard.Tests.Generation.Providers.OpenAiCompatible;

public sealed class OpenAiCompatibleProviderOptionsTests
{
    [Fact]
    public void ConstructorNormalizesBaseUriAndBuildsChatCompletionsEndpoint()
    {
        var options = new OpenAiCompatibleProviderOptions(
            new Uri("http://localhost:1234/v1"),
            "local-model");

        Assert.Equal(
            new Uri("http://localhost:1234/v1/"),
            options.BaseUri);
        Assert.Equal(
            new Uri("http://localhost:1234/v1/chat/completions"),
            options.Endpoint);
        Assert.Equal("local-model", options.Model);
        Assert.Null(options.ApiKey);
    }

    [Fact]
    public void FromEnvironmentReadsDedicatedApiKeyVariable()
    {
        string? requestedVariable = null;

        var options = OpenAiCompatibleProviderOptions.FromEnvironment(
            new Uri("https://example.test/v1"),
            "model",
            variableName =>
            {
                requestedVariable = variableName;
                return "environment-secret";
            });

        Assert.Equal(
            OpenAiCompatibleProviderOptions.ApiKeyEnvironmentVariableName,
            requestedVariable);
        Assert.Equal("environment-secret", options.ApiKey);
    }

    [Fact]
    public void ConstructorRejectsRelativeOrUnsupportedEndpoint()
    {
        Assert.Throws<ArgumentException>(
            () => new OpenAiCompatibleProviderOptions(
                new Uri("/v1", UriKind.Relative),
                "model"));

        Assert.Throws<ArgumentException>(
            () => new OpenAiCompatibleProviderOptions(
                new Uri("ftp://example.test/v1"),
                "model"));
    }

    [Fact]
    public void ConstructorRejectsEndpointQueryOrFragment()
    {
        Assert.Throws<ArgumentException>(
            () => new OpenAiCompatibleProviderOptions(
                new Uri("https://example.test/v1?tenant=one"),
                "model"));

        Assert.Throws<ArgumentException>(
            () => new OpenAiCompatibleProviderOptions(
                new Uri("https://example.test/v1#fragment"),
                "model"));
    }

    [Fact]
    public void ConstructorRequiresModel()
    {
        Assert.Throws<ArgumentException>(
            () => new OpenAiCompatibleProviderOptions(
                new Uri("https://example.test/v1"),
                " "));
    }
}
