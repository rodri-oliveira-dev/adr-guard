using Xunit;

namespace AdrGuard.Agent.Tests;

public sealed class McpSessionTests
{
    [Fact]
    public async Task ToolDiscoveryUsesTheMcpTimeout()
    {
        var exception = await Assert.ThrowsAsync<AgentMcpException>(
            () => McpDiscoveryTimeout.RunAsync(
                async token =>
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    return true;
                },
                TimeSpan.FromMilliseconds(25),
                CancellationToken.None));

        Assert.Contains("discovery timed out", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallerCancellationDuringToolDiscoveryRemainsCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => McpDiscoveryTimeout.RunAsync(
                async token =>
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    return true;
                },
                TimeSpan.FromSeconds(5),
                cancellationSource.Token));
    }
}
