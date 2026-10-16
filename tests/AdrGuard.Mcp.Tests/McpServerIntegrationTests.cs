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
        var launch = ResolveServerLaunch();
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "ADR Guard integration test",
            Command = launch.Command,
            Arguments = [.. launch.Arguments, "--repository", repository.Root, "--adr-directory", "docs/adr"],
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
        var launch = ResolveServerLaunch();
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "ADR Guard invalid argument test",
            Command = launch.Command,
            Arguments = [.. launch.Arguments, "--repository", repository.Root],
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

    [Fact]
    public async Task InstalledServerExecutesImpactAndDriftAgainstControlledFixtures()
    {
        using var repository = TemporaryMcpRepository.Create();
        repository.Write("docs/adr/0001-boundary.md", """
            # Protect the domain boundary

            ## Status

            Accepted

            ## Context

            The domain must remain independent.

            ## Decision

            Infrastructure may depend on Domain, never the reverse.

            ## Consequences

            Dependency direction is verified deterministically.
            """);
        repository.Write("src/Domain/Domain.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        repository.Write("src/Infrastructure/Infrastructure.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        repository.Write(".adrguard-impact.json", ImpactManifest);
        repository.Write(".adrguard-fitness.json", FitnessManifest);
        repository.InitializeGit();
        repository.Write("src/Domain/change.txt", "changed");

        var launch = ResolveServerLaunch();
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "ADR Guard architecture smoke test",
            Command = launch.Command,
            Arguments = [.. launch.Arguments, "--repository", repository.Root, "--adr-directory", "docs/adr"],
        });

        await using var client = await McpClient.CreateAsync(
            transport,
            cancellationToken: TestContext.Current.CancellationToken);
        var impact = await client.CallToolAsync(
            "analyze_impact",
            new Dictionary<string, object?> { ["baseReference"] = "HEAD", ["manifestPath"] = ".adrguard-impact.json" },
            cancellationToken: TestContext.Current.CancellationToken);
        var drift = await client.CallToolAsync(
            "verify_architecture",
            new Dictionary<string, object?> { ["rulesPath"] = ".adrguard-fitness.json", ["policy"] = "advisory" },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(impact.IsError is not true, ReadText(impact));
        Assert.True(drift.IsError is not true, ReadText(drift));
        Assert.Contains("affectedDecisions", impact.Content.OfType<TextContentBlock>().Single().Text, StringComparison.Ordinal);
        Assert.Contains("domain-no-infrastructure", drift.Content.OfType<TextContentBlock>().Single().Text, StringComparison.Ordinal);
    }

    private static (string Command, string[] Arguments) ResolveServerLaunch()
    {
        var installedCommand = Environment.GetEnvironmentVariable("ADR_GUARD_MCP_TEST_COMMAND");
        return string.IsNullOrWhiteSpace(installedCommand)
            ? ("dotnet", [typeof(McpServerApplication).Assembly.Location])
            : (installedCommand, []);
    }

    private static string ReadText(CallToolResult result) =>
        string.Join(Environment.NewLine, result.Content.OfType<TextContentBlock>().Select(block => block.Text));

    private const string ImpactManifest = """
        {
          "schemaVersion": "1.0",
          "mappings": [
            {
              "decision": { "stableId": "ADR-1", "path": "docs/adr/0001-boundary.md" },
              "patterns": ["src/Domain/**"],
              "relationship": "governs",
              "reason": "The ADR governs the domain boundary."
            }
          ]
        }
        """;

    private const string FitnessManifest = """
        {
          "schemaVersion": "1.0",
          "rules": [
            {
              "id": "domain-no-infrastructure",
              "decision": { "stableId": "ADR-1", "path": "docs/adr/0001-boundary.md" },
              "scope": { "projects": ["src/Domain/Domain.csproj", "src/Infrastructure/Infrastructure.csproj"] },
              "check": { "type": "forbid-project-reference", "from": "src/Domain/Domain.csproj", "to": "src/Infrastructure/Infrastructure.csproj" },
              "evidence": { "source": "dotnet-project-reference" },
              "failure": { "severity": "error", "onInconclusive": "unknown" },
              "gate": true
            }
          ]
        }
        """;
}
