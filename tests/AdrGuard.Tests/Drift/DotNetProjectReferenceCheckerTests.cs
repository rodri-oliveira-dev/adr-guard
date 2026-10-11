using AdrGuard.Drift;
using Xunit;

namespace AdrGuard.Tests.Drift;

public sealed class DotNetProjectReferenceCheckerTests
{
    [Fact]
    public void ReturnsPassWhenCompleteStaticReferenceSetDoesNotContainForbiddenTarget()
    {
        using var repository = Repository.Create("<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var result = Assert.Single(DotNetProjectReferenceChecker.Check(repository.Manifest(), CancellationToken.None));
        Assert.Equal(FitnessOutcome.Pass, result.Outcome);
        Assert.Equal("deterministic-static", result.Confidence);
    }

    [Fact]
    public void ReturnsViolationWithExactEvidenceAndLineForForbiddenReference()
    {
        using var repository = Repository.Create("""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="../Infrastructure/Infrastructure.csproj" />
                <ProjectReference Include="../Infrastructure/Infrastructure.csproj" />
              </ItemGroup>
            </Project>
            """);
        var result = Assert.Single(DotNetProjectReferenceChecker.Check(repository.Manifest(), CancellationToken.None));
        Assert.Equal(FitnessOutcome.Violation, result.Outcome);
        Assert.Equal(2, result.Evidence.Count);
        Assert.All(result.Evidence, evidence => Assert.True(evidence.Line > 0));
    }

    [Fact]
    public void NormalizesWindowsStyleProjectReferenceOnEveryPlatform()
    {
        using var repository = Repository.Create("<Project><ItemGroup><ProjectReference Include=\"..\\Infrastructure\\Infrastructure.csproj\" /></ItemGroup></Project>");
        Assert.Equal(FitnessOutcome.Violation, Assert.Single(DotNetProjectReferenceChecker.Check(repository.Manifest(), CancellationToken.None)).Outcome);
    }

    [Theory]
    [InlineData("<Project><ItemGroup Condition=\"'$(Configuration)' == 'Debug'\"><ProjectReference Include=\"../Infrastructure/Infrastructure.csproj\" /></ItemGroup></Project>")]
    [InlineData("<Project><ItemGroup><ProjectReference Include=\"$(TargetProject)\" /></ItemGroup></Project>")]
    [InlineData("<Project><ItemGroup><ProjectReference Include=\"@(ProjectReferences)\" /></ItemGroup></Project>")]
    [InlineData("<Project><ItemGroup><ProjectReference Include=\"%(Identity)\" /></ItemGroup></Project>")]
    [InlineData("<Project><ItemGroup><ProjectReference Include=\"../Infrastructure/Infrastructure.csproj;../Other/Other.csproj\" /></ItemGroup></Project>")]
    [InlineData("<Project><ItemGroup><ProjectReference Include=\"../*/Infrastructure.csproj\" /></ItemGroup></Project>")]
    [InlineData("<Project><ItemGroup><ProjectReference Include=\"../Infrastructure/Infrastructure.csproj\" Exclude=\"../Infrastructure/Infrastructure.csproj\" /></ItemGroup></Project>")]
    public void ReturnsUnknownForConditionalOrDynamicReferences(string xml)
    {
        using var repository = Repository.Create(xml);
        var result = Assert.Single(DotNetProjectReferenceChecker.Check(repository.Manifest(), CancellationToken.None));
        Assert.Equal(FitnessOutcome.Unknown, result.Outcome);
        Assert.NotEmpty(result.Evidence);
    }

    [Fact]
    public void ReturnsUnknownWhenProjectDeclaresExplicitImport()
    {
        using var repository = Repository.Create("<Project><Import Project=\"Dependencies.props\" /></Project>");
        var result = Assert.Single(DotNetProjectReferenceChecker.Check(repository.Manifest(), CancellationToken.None));
        Assert.Equal(FitnessOutcome.Unknown, result.Outcome);
        Assert.Contains(result.Evidence, evidence => evidence.Kind == "project-import");
    }

    [Theory]
    [InlineData("Directory.Build.props")]
    [InlineData("Directory.Build.targets")]
    public void ReturnsUnknownWhenAncestorDeclaresImplicitBuildImport(string fileName)
    {
        using var repository = Repository.Create("<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(repository.Root, fileName), "<Project />");
        var result = Assert.Single(DotNetProjectReferenceChecker.Check(repository.Manifest(), CancellationToken.None));
        Assert.Equal(FitnessOutcome.Unknown, result.Outcome);
        Assert.Contains(result.Evidence, evidence => evidence.Kind == "project-import" && evidence.Target == fileName);
    }

    [Theory]
    [InlineData("<!DOCTYPE Project [<!ENTITY xxe SYSTEM \"file:///etc/passwd\">]><Project>&xxe;</Project>")]
    [InlineData("<Project><ItemGroup>")]
    public void UnsafeOrMalformedXmlIsUnknownNeverPass(string xml)
    {
        using var repository = Repository.Create(xml);
        Assert.Equal(FitnessOutcome.Unknown, Assert.Single(DotNetProjectReferenceChecker.Check(repository.Manifest(), CancellationToken.None)).Outcome);
    }

    [Fact]
    public void MissingProjectIsUnknownAndInactiveDecisionIsNotApplicable()
    {
        using var repository = Repository.Create("<Project />");
        File.Delete(Path.Combine(repository.Root, "src", "Domain", "Domain.csproj"));
        Assert.Equal(FitnessOutcome.Unknown, Assert.Single(DotNetProjectReferenceChecker.Check(repository.Manifest(), CancellationToken.None)).Outcome);
        Assert.Equal(FitnessOutcome.NotApplicable, Assert.Single(DotNetProjectReferenceChecker.Check(repository.Manifest(FitnessDecisionState.Inactive), CancellationToken.None)).Outcome);
    }

    [Fact]
    public void CancellationIsObservedBeforeReadingProjects()
    {
        using var repository = Repository.Create("<Project />");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => DotNetProjectReferenceChecker.Check(repository.Manifest(), cancellation.Token));
    }

    [Fact]
    public void InvalidUtf8AndEscapingReferenceAreUnknown()
    {
        using var repository = Repository.Create("<Project />");
        File.WriteAllBytes(Path.Combine(repository.Root, "src", "Domain", "Domain.csproj"), [0xff, 0xfe]);
        Assert.Equal(FitnessOutcome.Unknown, Assert.Single(DotNetProjectReferenceChecker.Check(repository.Manifest(), CancellationToken.None)).Outcome);

        File.WriteAllText(Path.Combine(repository.Root, "src", "Domain", "Domain.csproj"), "<Project><ItemGroup><ProjectReference Include=\"../../../outside.csproj\" /></ItemGroup></Project>");
        Assert.Equal(FitnessOutcome.Unknown, Assert.Single(DotNetProjectReferenceChecker.Check(repository.Manifest(), CancellationToken.None)).Outcome);
    }

    [Fact]
    public void ContractFixtureLoadsWithEffectiveLifecycleAndStableOrdering()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Drift", "contract");
        var manifest = FitnessManifestLoader.Load(root, ".adrguard-fitness.json");
        Assert.Equal(["architecture-tests", "domain-no-infrastructure"], manifest.Rules.Select(rule => rule.Id).ToArray());
        Assert.All(manifest.Rules, rule => Assert.Equal(FitnessDecisionState.Effective, rule.Decision.State));
    }

    [Theory]
    [InlineData("Proposed")]
    [InlineData("Rejected")]
    [InlineData("Deprecated")]
    [InlineData("Superseded")]
    public void NonEffectiveLifecycleStatusesAreNotApplicable(string status)
    {
        using var repository = ContractRepository.Copy();
        repository.ReplaceStatus(status);
        var manifest = FitnessManifestLoader.Load(repository.Root, ".adrguard-fitness.json");
        Assert.All(manifest.Rules, rule => Assert.Equal(FitnessDecisionState.Inactive, rule.Decision.State));
        Assert.Equal(FitnessOutcome.NotApplicable, Assert.Single(DotNetProjectReferenceChecker.Check(manifest, CancellationToken.None)).Outcome);
    }

    [Fact]
    public void CustomAcceptedLifecycleAliasIsEffective()
    {
        using var repository = ContractRepository.Copy();
        repository.ReplaceStatus("Approved");
        File.WriteAllText(Path.Combine(repository.Root, ".adrguard.yml"), "schema-version: 1\nlifecycle-statuses: Approved=accepted\n");
        var manifest = FitnessManifestLoader.Load(repository.Root, ".adrguard-fitness.json");
        Assert.All(manifest.Rules, rule => Assert.Equal(FitnessDecisionState.Effective, rule.Decision.State));
    }

    [Fact]
    public void ManifestRejectsUnknownFieldsAndTraversal()
    {
        using var repository = ContractRepository.Copy();
        var path = Path.Combine(repository.Root, ".adrguard-fitness.json");
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"schemaVersion\": \"1.0\"", "\"schemaVersion\": \"1.0\", \"unknown\": true"));
        Assert.Throws<FitnessManifestException>(() => FitnessManifestLoader.Load(repository.Root, ".adrguard-fitness.json"));
        Assert.Throws<FitnessManifestException>(() => FitnessManifestLoader.Load(repository.Root, "../rules.json"));
    }

    [Fact]
    public void ManifestRejectsRuleWithMissingGate()
    {
        using var repository = ContractRepository.Copy();
        var path = Path.Combine(repository.Root, ".adrguard-fitness.json");
        var manifest = File.ReadAllText(path)
            .Replace(",\r\n      \"gate\": true", string.Empty)
            .Replace(",\n      \"gate\": true", string.Empty);
        File.WriteAllText(path, manifest);
        Assert.Throws<FitnessManifestException>(() => FitnessManifestLoader.Load(repository.Root, ".adrguard-fitness.json"));
    }

    private sealed class Repository : IDisposable
    {
        private Repository(string root) => Root = root;
        internal string Root { get; }

        internal static Repository Create(string domainXml)
        {
            var root = Path.Combine(Path.GetTempPath(), $"adr-guard-project-check-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(root, "src", "Domain"));
            Directory.CreateDirectory(Path.Combine(root, "src", "Infrastructure"));
            File.WriteAllText(Path.Combine(root, "src", "Domain", "Domain.csproj"), domainXml);
            File.WriteAllText(Path.Combine(root, "src", "Infrastructure", "Infrastructure.csproj"), "<Project />");
            return new Repository(root);
        }

        internal FitnessManifest Manifest(FitnessDecisionState state = FitnessDecisionState.Effective)
        {
            var decision = new FitnessDecision("ADR-1", "docs/adr/0001.md", state, state == FitnessDecisionState.Effective ? "Accepted" : "Rejected", null);
            var rule = new FitnessRule("domain-no-infrastructure", decision,
                ["src/Domain/Domain.csproj", "src/Infrastructure/Infrastructure.csproj"],
                FitnessCheckType.ForbidProjectReference, "src/Domain/Domain.csproj", "src/Infrastructure/Infrastructure.csproj", null,
                "dotnet-project-reference", "error", true);
            return new FitnessManifest(Root, ".adrguard-fitness.json", [rule]);
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    private sealed class ContractRepository : IDisposable
    {
        private ContractRepository(string root) => Root = root;
        internal string Root { get; }
        internal static ContractRepository Copy()
        {
            var source = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Drift", "contract");
            var root = Path.Combine(Path.GetTempPath(), $"adr-guard-contract-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(Path.Combine(root, Path.GetRelativePath(source, directory)));
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)) File.Copy(file, Path.Combine(root, Path.GetRelativePath(source, file)));
            return new(root);
        }
        internal void ReplaceStatus(string status)
        {
            var path = Path.Combine(Root, "docs", "adr", "0001-protect-domain-boundary.md");
            var markdown = File.ReadAllText(path).ReplaceLineEndings("\n");
            File.WriteAllText(path, markdown.Replace("## Status\n\nAccepted", $"## Status\n\n{status}"));
        }
        public void Dispose() => Directory.Delete(Root, true);
    }
}
