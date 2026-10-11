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
    [InlineData("<Project><ItemGroup><ProjectReference Include=\"../*/Infrastructure.csproj\" /></ItemGroup></Project>")]
    public void ReturnsUnknownForConditionalOrDynamicReferences(string xml)
    {
        using var repository = Repository.Create(xml);
        var result = Assert.Single(DotNetProjectReferenceChecker.Check(repository.Manifest(), CancellationToken.None));
        Assert.Equal(FitnessOutcome.Unknown, result.Outcome);
        Assert.NotEmpty(result.Evidence);
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
}
