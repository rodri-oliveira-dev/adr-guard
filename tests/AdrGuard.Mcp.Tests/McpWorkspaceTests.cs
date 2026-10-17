using ModelContextProtocol;
using Xunit;

namespace AdrGuard.Mcp.Tests;

public sealed class McpWorkspaceTests
{
    private const string AcceptedAdr = """
        # Use a safe boundary

        ## Status

        Accepted

        ## Context

        The service needs deterministic boundaries.

        ## Decision

        Keep the domain independent.

        ## Consequences

        Changes remain reviewable.
        """;

    [Fact]
    public void ListGetAndSearchReturnDeterministicBoundedContracts()
    {
        using var repository = TemporaryMcpRepository.Create();
        repository.Write("docs/adr/0001-safe-boundary.md", AcceptedAdr);
        repository.Write("docs/adr/0002-cache.md", AcceptedAdr.Replace("safe boundary", "cache", StringComparison.Ordinal));
        var workspace = CreateWorkspace(repository);

        var list = workspace.ListAdrs(TestContext.Current.CancellationToken);
        var get = workspace.GetAdr("ADR-1", TestContext.Current.CancellationToken);
        var search = workspace.SearchAdrs("safe boundary", 10, TestContext.Current.CancellationToken);

        Assert.Equal("1.0", list.SchemaVersion);
        Assert.Equal(["ADR-1", "ADR-2"], list.Adrs.Select(item => item.Identifier));
        Assert.Equal("docs/adr/0001-safe-boundary.md", get.Adr.Path);
        Assert.True(get.ContentIsUntrusted);
        Assert.Contains("Keep the domain independent", get.Content, StringComparison.Ordinal);
        Assert.Single(search.Matches);
        Assert.True(search.ExcerptsAreUntrusted);
        Assert.Contains("content", search.Matches[0].MatchedFields);
    }

    [Fact]
    public void RelationshipsDistinguishValidAndUnresolvedTargets()
    {
        using var repository = TemporaryMcpRepository.Create();
        repository.Write("docs/adr/0001-safe-boundary.md", AcceptedAdr + "\n## Depends on\n\n[Cache](0002-cache.md) and [missing](0099-missing.md).\n");
        repository.Write("docs/adr/0002-cache.md", AcceptedAdr.Replace("safe boundary", "cache", StringComparison.Ordinal));
        var result = CreateWorkspace(repository).GetRelationships("ADR-1", TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Relationships.Count);
        Assert.Contains(result.Relationships, item => item.Resolution == "valid" && item.TargetIdentifier == "ADR-2");
        Assert.Contains(result.Relationships, item => item.Resolution == "unresolved");
        Assert.Contains(result.Issues, item => item.Code == "ADR007");
    }

    [Fact]
    public void RelationshipsResolveNestedLinksWithinRepositoryRoot()
    {
        using var repository = TemporaryMcpRepository.Create();
        repository.Write("docs/adr/nested/0001-safe-boundary.md", AcceptedAdr + "\n## Depends on\n\n[Cache](../shared/0002-cache.md).\n");
        repository.Write("docs/adr/shared/0002-cache.md", AcceptedAdr.Replace("safe boundary", "cache", StringComparison.Ordinal));

        var result = CreateWorkspace(repository).GetRelationships("ADR-1", TestContext.Current.CancellationToken);

        var relationship = Assert.Single(result.Relationships);
        Assert.Equal("valid", relationship.Resolution);
        Assert.Equal("ADR-2", relationship.TargetIdentifier);
        Assert.Equal("docs/adr/shared/0002-cache.md", relationship.TargetPath);
        Assert.DoesNotContain(result.Issues, item => item.Code == "ADR007");
    }

    [Fact]
    public void ValidationDiagnosticsAreSuccessfulDomainResults()
    {
        using var repository = TemporaryMcpRepository.Create();
        repository.Write("docs/adr/0001-invalid.md", "# Incomplete\n\n## Status\n\nAccepted\n");

        var result = CreateWorkspace(repository).ValidateAdrs(null, TestContext.Current.CancellationToken);

        Assert.False(result.Valid);
        Assert.Equal(1, result.Files);
        Assert.Contains(result.Diagnostics, item => item.Code == "ADR005" && item.Severity == "error");
    }

    [Fact]
    public void WorkspaceRejectsTraversalOversizedFilesAndCancellation()
    {
        using var repository = TemporaryMcpRepository.Create();
        repository.Write("docs/adr/0001-safe-boundary.md", AcceptedAdr);
        repository.WriteBytes("docs/adr/0002-large.md", new byte[McpWorkspace.MaximumAdrBytes + 1]);
        var workspace = CreateWorkspace(repository);

        Assert.Throws<McpServerConfigurationException>(() =>
            McpServerOptions.Parse(["--repository", repository.Root, "--adr-directory", "../outside"]));
        Assert.Throws<InvalidDataException>(() => workspace.GetAdr("../outside.md", TestContext.Current.CancellationToken));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => workspace.ListAdrs(cancellation.Token));
    }

    [Fact]
    public void SymlinkedAdrIsNeverRead()
    {
        using var repository = TemporaryMcpRepository.Create();
        var outside = Path.Combine(Path.GetTempPath(), $"adr-guard-mcp-outside-{Guid.NewGuid():N}.md");
        File.WriteAllText(outside, AcceptedAdr);
        var link = Path.Combine(repository.Root, "docs", "adr", "0001-linked.md");
        try
        {
            try
            {
                File.CreateSymbolicLink(link, outside);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
            {
                return;
            }

            Assert.Empty(CreateWorkspace(repository).ListAdrs(TestContext.Current.CancellationToken).Adrs);
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public void ExcessiveStructuredResponseIsRejected()
    {
        var oversized = new { Content = new string('x', McpResponseLimit.MaximumResponseBytes) };

        var exception = Assert.Throws<McpException>(() => McpResponseLimit.Ensure(oversized));

        Assert.Contains("response exceeds", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SnapshotIncludesPersistentGitMetadataButIgnoresTransientLockFiles()
    {
        using var repository = TemporaryMcpRepository.Create();
        repository.Write("docs/adr/0001-safe-boundary.md", AcceptedAdr);
        repository.InitializeGit();

        var before = repository.Snapshot();
        Assert.Contains(".git/config", before.Keys);
        Assert.Contains(".git/index", before.Keys);

        var lockPath = Path.Combine(repository.Root, ".git", "objects", "maintenance.lock");
        File.WriteAllText(lockPath, "transient");
        var withLock = repository.Snapshot();
        Assert.Equal(before.Count, withLock.Count);
        Assert.Equal(before[".git/config"], withLock[".git/config"]);
        File.Delete(lockPath);

        File.AppendAllText(Path.Combine(repository.Root, ".git", "config"), "\n# changed\n");
        var after = repository.Snapshot();
        Assert.NotEqual(before[".git/config"], after[".git/config"]);
        Assert.Equal(before[".git/index"], after[".git/index"]);

        using (var indexStream = new FileStream(
                   Path.Combine(repository.Root, ".git", "index"),
                   FileMode.Append,
                   FileAccess.Write,
                   FileShare.None))
        {
            indexStream.WriteByte(0);
        }

        Assert.NotEqual(after[".git/index"], repository.Snapshot()[".git/index"]);
    }

    [Fact]
    public void ImpactAndDriftReuseExistingReadOnlyEnginesWithoutWriting()
    {
        using var repository = TemporaryMcpRepository.Create();
        repository.Write("docs/adr/0001-safe-boundary.md", AcceptedAdr);
        repository.Write("src/Domain/Domain.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        repository.Write("src/Infrastructure/Infrastructure.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        repository.Write(".adrguard-impact.json", ImpactManifest);
        repository.Write(".adrguard-fitness.json", FitnessManifest);
        repository.InitializeGit();
        repository.Write("src/Domain/change.txt", "changed");
        var workspace = CreateWorkspace(repository);
        var before = repository.Snapshot();

        var impact = workspace.AnalyzeImpact("HEAD", ".adrguard-impact.json", TestContext.Current.CancellationToken);
        var drift = workspace.VerifyArchitecture(".adrguard-fitness.json", null, null, "advisory", TestContext.Current.CancellationToken);

        Assert.True(impact.Advisory);
        Assert.Equal(1, impact.Summary.AffectedDecisions);
        Assert.Equal(1, drift.Coverage.Pass);
        Assert.False(drift.Inconclusive);
        Assert.Equal(before, repository.Snapshot());
    }

    private static McpWorkspace CreateWorkspace(TemporaryMcpRepository repository) =>
        new(new McpServerOptions(repository.Root, "docs/adr", TimeSpan.FromSeconds(10)));

    private const string ImpactManifest = """
        {
          "schemaVersion": "1.0",
          "mappings": [
            {
              "decision": { "stableId": "ADR-1", "path": "docs/adr/0001-safe-boundary.md" },
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
              "decision": { "stableId": "ADR-1", "path": "docs/adr/0001-safe-boundary.md" },
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
