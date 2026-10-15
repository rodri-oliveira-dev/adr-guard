using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AdrGuard.Mcp.Tests;

public sealed class McpServerIntegrationTests
{
    [Fact]
    public async Task StdioClientDiscoversTypedReadOnlyToolsAndExecutesRealOperation()
    {
        using var repository = TemporaryMcpRepository.Create();
        repository.Write("docs/adr/0001-integration.md", """
            # MCP integration

            ## Status

            Accepted

            ## Context

            Clients need architecture context.

            ## Decision

            Expose bounded read-only tools.

            ## Consequences

            The CLI remains authoritative.
            """);
        var serverAssembly = typeof(McpServerApplication).Assembly.Location;
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "ADR Guard integration test",
            Command = "dotnet",
            Arguments = [serverAssembly, "--repository", repository.Root, "--adr-directory", "docs/adr"],
        });

        await using var client = await McpClient.CreateAsync(
            transport,
            cancellationToken: TestContext.Current.CancellationToken);
        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            ["analyze_impact", "get_adr", "get_adr_relationships", "list_adrs", "search_adrs", "validate_adrs", "verify_architecture"],
            tools.Select(tool => tool.Name).Order(StringComparer.Ordinal));
        Assert.All(tools, tool =>
        {
            Assert.True(tool.ProtocolTool.Annotations?.ReadOnlyHint);
            Assert.NotNull(tool.ProtocolTool.OutputSchema);
        });

        var result = await client.CallToolAsync(
            "list_adrs",
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        Assert.Contains(
            "ADR-1",
            result.Content.OfType<TextContentBlock>().Single().Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidArgumentsProduceProtocolErrorAndServerRemainsUsable()
    {
        using var repository = TemporaryMcpRepository.Create();
        repository.Write("docs/adr/0001-integration.md", "# Integration\n");
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "ADR Guard invalid argument test",
            Command = "dotnet",
            Arguments = [typeof(McpServerApplication).Assembly.Location, "--repository", repository.Root],
        });

        await using var client = await McpClient.CreateAsync(
            transport,
            cancellationToken: TestContext.Current.CancellationToken);
        await Assert.ThrowsAnyAsync<Exception>(async () => await client.CallToolAsync(
            "search_adrs",
            new Dictionary<string, object?> { ["query"] = "", ["limit"] = 20 },
            cancellationToken: TestContext.Current.CancellationToken));

        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(7, tools.Count);
    }
}
