using System.Text.Json;
using Xunit;

namespace AdrGuard.Agent.Tests;

public sealed class AdrGuardMcpClientTests
{
    [Fact]
    public async Task SevenAdaptersPreserveToolArgumentsEvidenceAndUncertainty()
    {
        var session = new FakeMcpSession(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["list_adrs"] = """{"schemaVersion":"1.0","total":0,"adrs":[]}""",
            ["get_adr"] = """{"schemaVersion":"1.0","adr":{},"contentIsUntrusted":true,"content":"untrusted"}""",
            ["search_adrs"] = """{"schemaVersion":"1.0","query":"boundary","excerptsAreUntrusted":true,"total":0,"matches":[]}""",
            ["get_adr_relationships"] = """{"schemaVersion":"1.0","relationships":[],"issues":[]}""",
            ["validate_adrs"] = """{"schemaVersion":"1.0","valid":false,"files":1,"policy":{},"diagnostics":[]}""",
            ["analyze_impact"] = """{"schemaVersion":"1.0","advisory":true,"repository":{},"summary":{},"coverage":{},"uncertainty":{"hasUnknowns":true},"changedFiles":[],"decisions":[]}""",
            ["verify_architecture"] = """{"schemaVersion":"1.0","policy":"advisory","repository":{},"coverage":{},"findings":[],"inconclusive":true,"policyFailed":false}""",
        });
        await using var client = await ConnectAsync(session);

        var evidence = new[]
        {
            await client.ListAdrsAsync(TestContext.Current.CancellationToken),
            await client.GetAdrAsync("ADR-1", TestContext.Current.CancellationToken),
            await client.SearchAdrsAsync("boundary", 10, TestContext.Current.CancellationToken),
            await client.GetAdrRelationshipsAsync("ADR-1", TestContext.Current.CancellationToken),
            await client.ValidateAdrsAsync("strict", TestContext.Current.CancellationToken),
            await client.AnalyzeImpactAsync("HEAD", ".adrguard-impact.json", TestContext.Current.CancellationToken),
            await client.VerifyArchitectureAsync(
                ".adrguard-fitness.json",
                ["artifacts/evidence.json"],
                ".adrguard-baseline.json",
                "advisory",
                TestContext.Current.CancellationToken),
        };

        Assert.Equal(
            ["list_adrs", "get_adr", "search_adrs", "get_adr_relationships", "validate_adrs", "analyze_impact", "verify_architecture"],
            evidence.Select(item => item.ToolName));
        Assert.All(evidence, item => Assert.Equal("1.0", item.SchemaVersion));
        Assert.False(evidence[0].ContentIsUntrusted);
        Assert.True(evidence[1].ContentIsUntrusted);
        Assert.True(evidence[2].ContentIsUntrusted);
        Assert.Equal(McpEvidenceStatus.Findings, evidence[4].Status);
        Assert.Equal(McpEvidenceStatus.Inconclusive, evidence[5].Status);
        Assert.Equal(McpEvidenceStatus.Inconclusive, evidence[6].Status);
        Assert.Equal("ADR-1", session.Calls[1].Arguments["identifier"]);
        Assert.Equal(10, session.Calls[2].Arguments["limit"]);
        Assert.Equal("strict", session.Calls[4].Arguments["validationProfile"]);
        Assert.Equal("HEAD", session.Calls[5].Arguments["baseReference"]);
        Assert.Equal("advisory", session.Calls[6].Arguments["policy"]);
        Assert.Contains("\"manifestPath\":\".adrguard-impact.json\"", evidence[5].ArgumentsJson, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"schemaVersion":"2.0","total":0,"adrs":[]}""", "unsupported schema version")]
    [InlineData("""{"schemaVersion":"1.0","total":0,"adrs":[],"unexpected":true}""", "unsupported evidence shape")]
    [InlineData("""{"schemaVersion":"1.0","total":"zero","adrs":[]}""", "unsupported evidence shape")]
    [InlineData("""["not-an-object"]""", "unsupported evidence shape")]
    [InlineData("""{"schemaVersion":"1.0","schemaVersion":"1.0","total":0,"adrs":[]}""", "unsupported evidence shape")]
    public async Task UnsupportedSchemaOrShapeFailsClosed(string json, string expectedMessage)
    {
        await using var client = await ConnectAsync(new FakeMcpSession("list_adrs", json));

        var exception = await Assert.ThrowsAsync<AgentMcpException>(
            () => client.ListAdrsAsync(TestContext.Current.CancellationToken));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExcessiveResponseFailsClosed()
    {
        var content = new string('x', AdrGuardMcpClient.MaximumResponseBytes);
        var json = JsonSerializer.Serialize(new
        {
            schemaVersion = "1.0",
            adr = new { },
            contentIsUntrusted = true,
            content,
        });
        await using var client = await ConnectAsync(new FakeMcpSession("get_adr", json));

        var exception = await Assert.ThrowsAsync<AgentMcpException>(
            () => client.GetAdrAsync("ADR-1", TestContext.Current.CancellationToken));

        Assert.Contains("evidence limit", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProtocolErrorIsNotReturnedAsEvidence()
    {
        var session = new FakeMcpSession("list_adrs", "{}") { ReturnError = true };
        await using var client = await ConnectAsync(session);

        var exception = await Assert.ThrowsAsync<AgentMcpException>(
            () => client.ListAdrsAsync(TestContext.Current.CancellationToken));

        Assert.Contains("returned an error", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OperationTimeoutIsExplicitAndSessionIsDisposed()
    {
        var session = new FakeMcpSession("list_adrs", "{}") { WaitForCancellation = true };
        await using (var client = await ConnectAsync(session, TimeSpan.FromMilliseconds(25)))
        {
            var exception = await Assert.ThrowsAsync<AgentMcpException>(
                () => client.ListAdrsAsync(CancellationToken.None));
            Assert.Contains("timed out", exception.Message, StringComparison.Ordinal);
        }

        Assert.True(session.Disposed);
    }

    [Fact]
    public async Task CallerCancellationRemainsCancellation()
    {
        var session = new FakeMcpSession("list_adrs", "{}") { WaitForCancellation = true };
        await using var client = await ConnectAsync(session);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.ListAdrsAsync(cancellationSource.Token));
    }

    private static async Task<AdrGuardMcpClient> ConnectAsync(
        FakeMcpSession session,
        TimeSpan? timeout = null)
    {
        var configuration = new AgentConfiguration(
            Path.GetFullPath("."),
            "docs/adr",
            "fake-mcp",
            [],
            timeout ?? TimeSpan.FromSeconds(5),
            "openai",
            "fake-model",
            "What decisions exist?",
            true,
            AgentLimits.Default,
            null,
            "HEAD",
            null,
            [],
            null,
            "advisory");
        return await AdrGuardMcpClient.ConnectAsync(
            configuration,
            new FakeMcpSessionFactory(session),
            TestContext.Current.CancellationToken);
    }

    private sealed class FakeMcpSessionFactory(FakeMcpSession session) : IMcpSessionFactory
    {
        public Task<IMcpSession> ConnectAsync(
            AgentConfiguration configuration,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IMcpSession>(session);
        }
    }

    private sealed class FakeMcpSession : IMcpSession
    {
        private readonly IReadOnlyDictionary<string, string> responses;

        internal FakeMcpSession(string toolName, string response)
            : this(new Dictionary<string, string>(StringComparer.Ordinal) { [toolName] = response })
        {
        }

        internal FakeMcpSession(IReadOnlyDictionary<string, string> responses)
        {
            this.responses = responses;
        }

        internal List<(string ToolName, IReadOnlyDictionary<string, object?> Arguments)> Calls { get; } = [];

        internal bool Disposed { get; private set; }

        internal bool ReturnError { get; init; }

        internal bool WaitForCancellation { get; init; }

        public async Task<McpRawToolResult> CallToolAsync(
            string toolName,
            IReadOnlyDictionary<string, object?> arguments,
            CancellationToken cancellationToken)
        {
            Calls.Add((toolName, arguments));
            if (WaitForCancellation)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            using var document = JsonDocument.Parse(responses[toolName]);
            return new McpRawToolResult(ReturnError, document.RootElement.Clone());
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
