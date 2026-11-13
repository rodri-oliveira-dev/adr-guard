using System.Diagnostics;
using Xunit;

namespace AdrGuard.Agent.Tests;

public sealed class AdrGuardMcpProcessIntegrationTests
{
    [Fact]
    public async Task LocalStdioClientUsesAllSevenRealReadOnlyTools()
    {
        using var repository = TemporaryRepository.Create();
        repository.Write("docs/adr/0001-boundary.md", """
            # Protect the domain boundary

            ## Status

            Accepted

            ## Context

            The domain needs a stable boundary.

            ## Decision

            Infrastructure may depend on Domain, never the reverse.

            ## Consequences

            The dependency direction is checked from bounded evidence.
            """);
        repository.Write(
            "src/Domain/Domain.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        repository.Write(
            "src/Infrastructure/Infrastructure.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        repository.Write(".adrguard-impact.json", ImpactManifest);
        repository.Write(".adrguard-fitness.json", FitnessManifest);
        repository.InitializeGit();
        repository.Write("src/Domain/change.txt", "changed");

        var configuration = CreateConfiguration(repository.Root);

        await using var client = await AdrGuardMcpClient.ConnectAsync(
            configuration,
            cancellationToken: TestContext.Current.CancellationToken);
        var evidence = new[]
        {
            await client.ListAdrsAsync(TestContext.Current.CancellationToken),
            await client.GetAdrAsync("ADR-1", TestContext.Current.CancellationToken),
            await client.SearchAdrsAsync("boundary", 10, TestContext.Current.CancellationToken),
            await client.GetAdrRelationshipsAsync(null, TestContext.Current.CancellationToken),
            await client.ValidateAdrsAsync(null, TestContext.Current.CancellationToken),
            await client.AnalyzeImpactAsync("HEAD", ".adrguard-impact.json", TestContext.Current.CancellationToken),
            await client.VerifyArchitectureAsync(
                ".adrguard-fitness.json",
                null,
                null,
                "advisory",
                TestContext.Current.CancellationToken),
        };

        Assert.Equal(McpToolCatalog.ToolNames, evidence.Select(item => item.ToolName).Order(StringComparer.Ordinal));
        Assert.All(evidence, item => Assert.Equal("1.0", item.SchemaVersion));
        Assert.Contains("ADR-1", evidence[0].ContentJson, StringComparison.Ordinal);
        Assert.Contains("Protect the domain boundary", evidence[1].ContentJson, StringComparison.Ordinal);
        Assert.Contains("affectedDecisions", evidence[5].ContentJson, StringComparison.Ordinal);
        Assert.Contains("domain-no-infrastructure", evidence[6].ContentJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SeparateClientsKeepRepositoryEvidenceIsolated()
    {
        using var firstRepository = TemporaryRepository.Create();
        using var secondRepository = TemporaryRepository.Create();
        firstRepository.Write("docs/adr/0001-first.md", "# First repository\n");
        secondRepository.Write("docs/adr/0002-second.md", "# Second repository\n");

        await using var firstClient = await AdrGuardMcpClient.ConnectAsync(
            CreateConfiguration(firstRepository.Root),
            cancellationToken: TestContext.Current.CancellationToken);
        await using var secondClient = await AdrGuardMcpClient.ConnectAsync(
            CreateConfiguration(secondRepository.Root),
            cancellationToken: TestContext.Current.CancellationToken);
        var firstEvidence = await firstClient.ListAdrsAsync(TestContext.Current.CancellationToken);
        var secondEvidence = await secondClient.ListAdrsAsync(TestContext.Current.CancellationToken);

        Assert.Contains("First repository", firstEvidence.ContentJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Second repository", firstEvidence.ContentJson, StringComparison.Ordinal);
        Assert.Contains("Second repository", secondEvidence.ContentJson, StringComparison.Ordinal);
        Assert.DoesNotContain("First repository", secondEvidence.ContentJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfiguredRunnerReadsRealMcpWithoutProviderCredentialOrCli()
    {
        using var repository = TemporaryRepository.Create();
        repository.Write(
            "docs/adr/0001-local.md",
            "# Local evidence\n\n## Status\n\nProposed\n");
        var configuration = CreateConfiguration(repository.Root) with
        {
            Provider = null,
            Model = null,
            Question = "List all ADRs",
            AllowRemoteContext = false,
            AdrProposal = null,
        };
        var runner = new ConfiguredArchitectureQuestionRunner();

        var result = await runner.RunAsync(
            configuration,
            TestContext.Current.CancellationToken);

        Assert.Contains("Model: disabled", result.Message, StringComparison.Ordinal);
        Assert.Contains("list_adrs", result.Message, StringComparison.Ordinal);
        Assert.Contains("Local evidence", result.Message, StringComparison.Ordinal);
        Assert.Contains("No model request or file write", result.Message, StringComparison.Ordinal);
    }

    private static AgentConfiguration CreateConfiguration(string repositoryRoot)
    {
        var buildConfiguration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var serverAssembly = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                $"../../../../../src/AdrGuard.Mcp/bin/{buildConfiguration}/net10.0/adr-guard-mcp.dll"));
        Assert.True(File.Exists(serverAssembly), $"MCP server build output was not found at '{serverAssembly}'.");
        return new AgentConfiguration(
            repositoryRoot,
            "docs/adr",
            "dotnet",
            [serverAssembly],
            TimeSpan.FromSeconds(90),
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
    }

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

    private sealed class TemporaryRepository : IDisposable
    {
        private TemporaryRepository(string root)
        {
            Root = root;
        }

        internal string Root { get; }

        internal static TemporaryRepository Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"adr guard agent {Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            return new TemporaryRepository(root);
        }

        internal void Write(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        internal void InitializeGit()
        {
            RunGit("init");
            RunGit("config", "user.email", "agent-tests@example.invalid");
            RunGit("config", "user.name", "ADR Guard Agent Tests");
            RunGit("add", ".");
            RunGit("commit", "-m", "baseline");
        }

        public void Dispose()
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(Root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }

            Directory.Delete(Root, recursive: true);
        }

        private void RunGit(params string[] arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = Root,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.Environment["GIT_CONFIG_GLOBAL"] = OperatingSystem.IsWindows() ? "NUL" : "/dev/null";
            startInfo.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Unable to start Git for the controlled test repository.");
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(
                process.ExitCode == 0,
                $"Git failed with exit code {process.ExitCode}: {standardOutput}{standardError}");
        }
    }
}
