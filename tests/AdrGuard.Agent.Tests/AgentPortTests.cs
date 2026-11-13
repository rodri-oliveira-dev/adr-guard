using Xunit;

namespace AdrGuard.Agent.Tests;

public sealed class AgentPortTests
{
    [Fact]
    public async Task LanguageModelPortRemainsReplaceableWithLocalFake()
    {
        ILanguageModel languageModel = new FakeLanguageModel();

        var response = await languageModel.CompleteAsync(
            new LanguageModelRequest("fake-provider", "fake-model", "local prompt", 512),
            TestContext.Current.CancellationToken);

        Assert.Equal("local response", response.Content);
        Assert.Equal("fake-provider", response.Provider);
        Assert.Equal("fake-model", response.Model);
    }

    private sealed class FakeLanguageModel : ILanguageModel
    {
        public Task<LanguageModelResponse> CompleteAsync(
            LanguageModelRequest request,
            CancellationToken cancellationToken)
        {
            Assert.Equal("fake-provider", request.Provider);
            Assert.Equal("fake-model", request.Model);
            Assert.Equal("local prompt", request.Prompt);
            Assert.Equal(512, request.MaxOutputTokens);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new LanguageModelResponse(
                "local response",
                request.Provider,
                request.Model));
        }
    }
}
