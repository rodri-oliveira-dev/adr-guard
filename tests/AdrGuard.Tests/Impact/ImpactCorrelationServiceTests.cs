using AdrGuard.Git;
using AdrGuard.Impact;
using Xunit;

namespace AdrGuard.Tests.Impact;

public sealed class ImpactCorrelationServiceTests
{
    [Fact]
    public void CorrelatesAffectedUnmappedRenameDeletionAndMultipleDecisions()
    {
        var manifest = Manifest(
            Mapping("ADR-1", "src/api/**", ImpactDecisionResolutionKind.Active),
            Mapping("ADR-2", "src/api/**", ImpactDecisionResolutionKind.Proposed),
            Mapping("ADR-3", "src/legacy/**", ImpactDecisionResolutionKind.Inactive));
        var inventory = Inventory(
            Change(GitChangeKind.Modified, newPath: "src/api/orders.cs"),
            Change(GitChangeKind.Renamed, oldPath: "src/legacy/client.cs", newPath: "src/api/client.cs"),
            Change(GitChangeKind.Deleted, oldPath: "src/legacy/removed.cs"),
            Change(GitChangeKind.Added, newPath: "docs/unmapped.md"));

        var result = ImpactCorrelationService.Analyze(manifest, inventory);

        Assert.Equal(new ImpactCoverage(4, 2, 1, 1), result.Coverage);
        Assert.Equal(ImpactAssessmentStatus.Affected, Decision(result, "ADR-1").Status);
        Assert.Equal(ImpactAssessmentStatus.Affected, Decision(result, "ADR-2").Status);
        Assert.Equal(ImpactAssessmentStatus.Unknown, Decision(result, "ADR-3").Status);
        Assert.Contains(
            Decision(result, "ADR-1").Evidence,
            evidence => evidence.ChangeKind == GitChangeKind.Renamed
                && evidence.PathRole == ImpactEvidencePathRole.Current
                && evidence.Path == "src/api/client.cs");
        Assert.Contains(
            Decision(result, "ADR-3").Evidence,
            evidence => evidence.ChangeKind == GitChangeKind.Renamed
                && evidence.PathRole == ImpactEvidencePathRole.Previous
                && evidence.Path == "src/legacy/client.cs");
        Assert.Contains(
            Decision(result, "ADR-3").Evidence,
            evidence => evidence.ChangeKind == GitChangeKind.Deleted
                && evidence.Path == "src/legacy/removed.cs");

        var apiChange = Assert.Single(
            result.Changes,
            change => change.Change.NewPath == "src/api/orders.cs");
        Assert.Equal(["ADR-1", "ADR-2"], apiChange.DecisionStableIds);
        Assert.Equal(
            ImpactAssessmentStatus.NotMatched,
            Assert.Single(
                result.Changes,
                change => change.Change.NewPath == "docs/unmapped.md").Status);
    }

    [Theory]
    [InlineData("src/**", "src/a/b.cs", true)]
    [InlineData("src/*/api/*.cs", "src/orders/api/read.cs", true)]
    [InlineData("src/*/api/*.cs", "src/orders/internal/read.cs", false)]
    [InlineData("*.md", "README.md", true)]
    [InlineData("*.md", "docs/README.md", false)]
    [InlineData("src/**/generated", "src/generated", true)]
    [InlineData("src/**/generated", "src/a/b/generated", true)]
    public void MatchesBoundedGlobSubset(
        string pattern,
        string path,
        bool expected)
    {
        Assert.Equal(expected, ImpactPathMatcher.IsMatch(pattern, path));
    }

    [Fact]
    public void OutputIsDeterministicAcrossInputOrderingAndDuplicateEvidence()
    {
        var firstManifest = Manifest(
            Mapping("ADR-2", "src/shared/**", ImpactDecisionResolutionKind.Active),
            Mapping("ADR-1", "src/shared/**", ImpactDecisionResolutionKind.Active));
        var secondManifest = Manifest(
            Mapping("ADR-1", "src/shared/**", ImpactDecisionResolutionKind.Active),
            Mapping("ADR-2", "src/shared/**", ImpactDecisionResolutionKind.Active));
        var change = Change(GitChangeKind.Modified, newPath: "src/shared/file.cs");

        var first = ImpactCorrelationService.Analyze(firstManifest, Inventory(change, change));
        var second = ImpactCorrelationService.Analyze(secondManifest, Inventory(change, change));

        Assert.Equal(
            first.Decisions.Select(ShapeDecision),
            second.Decisions.Select(ShapeDecision));
        Assert.All(first.Decisions, decision => Assert.Single(decision.Evidence));
    }

    [Fact]
    public void UnresolvedDecisionProducesUnknownRatherThanComplianceOrViolation()
    {
        var manifest = Manifest(
            Mapping("ADR-9", "src/security/**", ImpactDecisionResolutionKind.Unknown));
        var result = ImpactCorrelationService.Analyze(
            manifest,
            Inventory(Change(GitChangeKind.Modified, newPath: "src/security/auth.cs")));

        Assert.Equal(ImpactAssessmentStatus.Unknown, Assert.Single(result.Decisions).Status);
        Assert.Equal(ImpactAssessmentStatus.Unknown, Assert.Single(result.Changes).Status);
        Assert.Equal(1, result.Coverage.UnknownChanges);
        Assert.Equal(0, result.Coverage.NotMatchedChanges);
    }

    [Fact]
    public void RejectsDifferentRepositoryRootsAndExcessiveMatchingComplexity()
    {
        var manifest = Manifest(
            Mapping("ADR-1", "src/**", ImpactDecisionResolutionKind.Active));
        var differentRoot = Inventory(Change(GitChangeKind.Modified, newPath: "src/file.cs")) with
        {
            RepositoryRoot = Path.Combine(Path.GetTempPath(), "different-root"),
        };
        Assert.Throws<ImpactAnalysisException>(
            () => ImpactCorrelationService.Analyze(manifest, differentRoot));

        var patterns = Enumerable.Range(0, 101)
            .Select(index => $"other/{index:D3}/**")
            .ToArray();
        var mappings = Enumerable.Range(0, 100)
            .Select(index => Mapping(
                $"ADR-{index}",
                patterns,
                ImpactDecisionResolutionKind.Active))
            .ToArray();
        var changes = Enumerable.Range(0, 100)
            .Select(index => Change(GitChangeKind.Modified, newPath: $"src/{index:D3}/file.cs"))
            .ToArray();

        Assert.Contains(
            "complexity limit",
            Assert.Throws<ImpactAnalysisException>(
                () => ImpactCorrelationService.Analyze(Manifest(mappings), Inventory(changes))).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void HighVolumeWithinBoundsHasStableCoverage()
    {
        var manifest = Manifest(
            Mapping("ADR-1", "src/mapped/**", ImpactDecisionResolutionKind.Active));
        var changes = Enumerable.Range(0, 1000)
            .Select(index => Change(
                GitChangeKind.Modified,
                newPath: index % 2 == 0
                    ? $"src/mapped/{index:D4}.cs"
                    : $"src/unmapped/{index:D4}.cs"))
            .ToArray();

        var result = ImpactCorrelationService.Analyze(manifest, Inventory(changes));

        Assert.Equal(new ImpactCoverage(1000, 500, 0, 500), result.Coverage);
        Assert.Equal(500, Assert.Single(result.Decisions).Evidence.Count);
    }

    private static string ShapeDecision(ImpactDecisionAssessment decision) =>
        $"{decision.Decision.StableId}|{decision.Status}|{string.Join(';', decision.Evidence.Select(evidence => $"{evidence.Path}:{evidence.Pattern}"))}";

    private static ImpactDecisionAssessment Decision(
        ImpactAnalysisResult result,
        string stableId) =>
        Assert.Single(result.Decisions, decision => decision.Decision.StableId == stableId);

    private static ImpactMappingManifest Manifest(params ImpactMapping[] mappings) =>
        new(
            "1.0",
            RepositoryRoot(),
            Path.Combine(RepositoryRoot(), ".adrguard-impact.json"),
            mappings);

    private static ImpactMapping Mapping(
        string stableId,
        string pattern,
        ImpactDecisionResolutionKind resolutionKind) =>
        Mapping(stableId, [pattern], resolutionKind);

    private static ImpactMapping Mapping(
        string stableId,
        IReadOnlyList<string> patterns,
        ImpactDecisionResolutionKind resolutionKind) =>
        new(
            new ImpactDecisionReference(stableId, $"docs/adr/{stableId}.md"),
            patterns,
            ImpactRelationship.Governs,
            "Explicit test mapping.",
            new ImpactDecisionResolution(
                resolutionKind,
                resolutionKind.ToString(),
                resolutionKind is ImpactDecisionResolutionKind.Active ? null : "Lifecycle caveat.",
                null));

    private static GitChangeInventory Inventory(params GitChangedPath[] changes) =>
        new(
            RepositoryRoot(),
            "main",
            "0123456789abcdef",
            changes);

    private static GitChangedPath Change(
        GitChangeKind kind,
        string? oldPath = null,
        string? newPath = null) =>
        new(
            GitChangeScope.Committed,
            kind,
            oldPath,
            newPath,
            kind is GitChangeKind.Renamed or GitChangeKind.Copied ? 100 : null);

    private static string RepositoryRoot() =>
        Path.GetFullPath(Path.Combine(Path.GetTempPath(), "adr-impact-correlation-root"));
}
