using AdrGuard.Drift;
using Xunit;

namespace AdrGuard.Tests.Drift;

public sealed class DriftEngineTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ViolationTransitionsFromNewToExistingThenResolvedOnlyOnPass()
    {
        using var repository = Repository.Create(ForbiddenReference);
        var first = DriftEngine.Analyze(repository.Manifest(), null, null, "abc123", Now, CancellationToken.None);
        Assert.Equal("new", Assert.Single(first.Findings).BaselineState);
        var baseline = DriftEngine.Snapshot(first, Now);

        var existing = DriftEngine.Analyze(repository.Manifest(), null, baseline, "abc123", Now, CancellationToken.None);
        Assert.Equal("existing", Assert.Single(existing.Findings).BaselineState);

        repository.WriteDomain("<Project />");
        var fixedAnalysis = DriftEngine.Analyze(repository.Manifest(), null, baseline, "def456", Now, CancellationToken.None);
        Assert.Equal(1, fixedAnalysis.Coverage.Pass);
        Assert.Contains(fixedAnalysis.Findings, x => x.BaselineState == "resolved");
    }

    [Fact]
    public void UnknownOrRemovedMappingNeverResolvesBaselineViolation()
    {
        using var repository = Repository.Create(ForbiddenReference);
        var baseline = DriftEngine.Snapshot(DriftEngine.Analyze(repository.Manifest(), null, null, null, Now, CancellationToken.None), Now);
        repository.WriteDomain("<Project><ItemGroup><ProjectReference Include=\"$(Dynamic)\" /></ItemGroup></Project>");
        var uncertain = DriftEngine.Analyze(repository.Manifest(), null, baseline, null, Now, CancellationToken.None);
        Assert.Equal(1, uncertain.Coverage.Unknown);
        Assert.Contains(uncertain.Findings, x => x.BaselineState == "unresolved-baseline");

        var removed = DriftEngine.Analyze(new FitnessManifest(repository.Root, ".adrguard-fitness.json", []), null, baseline, null, Now, CancellationToken.None);
        Assert.Contains(removed.Findings, x => x.BaselineState == "unresolved-baseline");
        Assert.DoesNotContain(removed.Findings, x => x.BaselineState == "resolved");
    }

    [Fact]
    public void RemappedRuleIdNeverResolvesBaselineViolation()
    {
        using var repository = Repository.Create(ForbiddenReference);
        var baseline = DriftEngine.Snapshot(DriftEngine.Analyze(repository.Manifest(), null, null, null, Now, CancellationToken.None), Now);
        repository.WriteDomain("<Project />");
        var other = "src/Other/Other.csproj";
        Directory.CreateDirectory(Path.Combine(repository.Root, "src", "Other"));
        File.WriteAllText(Path.Combine(repository.Root, "src", "Other", "Other.csproj"), "<Project />");

        var remapped = DriftEngine.Analyze(repository.Manifest(other), null, baseline, null, Now, CancellationToken.None);

        Assert.Equal(1, remapped.Coverage.Pass);
        Assert.Contains(remapped.Findings, x => x.BaselineState == "unresolved-baseline");
        Assert.DoesNotContain(remapped.Findings, x => x.BaselineState == "resolved");
    }

    [Fact]
    public void FingerprintAndSnapshotAreStableAcrossPresentationAndSerialization()
    {
        using var repository = Repository.Create(ForbiddenReference);
        var rule = Assert.Single(repository.Manifest().Rules);
        var first = DriftEngine.Fingerprint(rule, FitnessOutcome.Violation);
        var cosmeticallyChanged = rule with { Severity = "notice" };
        Assert.Equal(first, DriftEngine.Fingerprint(cosmeticallyChanged, FitnessOutcome.Violation));

        var analysis = DriftEngine.Analyze(repository.Manifest(), null, null, "abc123", Now, CancellationToken.None);
        var snapshot = DriftEngine.Snapshot(analysis, Now);
        Assert.Equal(DriftSnapshotService.Serialize(snapshot), DriftSnapshotService.Serialize(DriftEngine.Snapshot(analysis, Now)));
    }

    [Fact]
    public void SharedPolicyEvaluationPreservesAdvisoryAndEnforceSemantics()
    {
        using var repository = Repository.Create(ForbiddenReference);
        var manifest = repository.Manifest();
        var analysis = DriftEngine.Analyze(manifest, null, null, "abc123", Now, CancellationToken.None);

        var advisory = DriftPolicyEvaluator.Evaluate(analysis, manifest, DriftPolicy.Advisory);
        var enforced = DriftPolicyEvaluator.Evaluate(analysis, manifest, DriftPolicy.Enforce);

        Assert.False(advisory.Inconclusive);
        Assert.False(advisory.PolicyFailed);
        Assert.Equal("advisory", advisory.Report.Policy);
        Assert.True(enforced.PolicyFailed);
        Assert.Equal("enforce", enforced.Report.Policy);
        Assert.Equal("violation", Assert.Single(enforced.Report.Findings).Outcome);
    }

    [Fact]
    public void SharedPolicyEvaluationKeepsUnknownEvidenceInconclusive()
    {
        using var repository = Repository.Create("<Project><ItemGroup><ProjectReference Include=\"$(Dynamic)\" /></ItemGroup></Project>");
        var manifest = repository.Manifest();
        var analysis = DriftEngine.Analyze(manifest, null, null, null, Now, CancellationToken.None);

        var evaluation = DriftPolicyEvaluator.Evaluate(analysis, manifest, DriftPolicy.Enforce);

        Assert.True(evaluation.Inconclusive);
        Assert.False(evaluation.PolicyFailed);
        Assert.Equal("unknown", Assert.Single(evaluation.Report.Findings).Outcome);
    }

    [Fact]
    public void SharedPolicyEvaluationDoesNotFailForExistingBaselineViolation()
    {
        using var repository = Repository.Create(ForbiddenReference);
        var manifest = repository.Manifest();
        var baseline = DriftEngine.Snapshot(
            DriftEngine.Analyze(manifest, null, null, null, Now, CancellationToken.None),
            Now);
        var analysis = DriftEngine.Analyze(manifest, null, baseline, null, Now, CancellationToken.None);

        var evaluation = DriftPolicyEvaluator.Evaluate(analysis, manifest, DriftPolicy.Enforce);

        Assert.Equal("existing", Assert.Single(analysis.Findings).BaselineState);
        Assert.False(evaluation.Inconclusive);
        Assert.False(evaluation.PolicyFailed);
    }

    [Fact]
    public void SharedPolicyEvaluationDoesNotFailForNonGatingRule()
    {
        using var repository = Repository.Create(ForbiddenReference);
        var manifest = repository.Manifest(gate: false);
        var analysis = DriftEngine.Analyze(manifest, null, null, null, Now, CancellationToken.None);

        var evaluation = DriftPolicyEvaluator.Evaluate(analysis, manifest, DriftPolicy.Enforce);

        Assert.Equal("new", Assert.Single(analysis.Findings).BaselineState);
        Assert.False(evaluation.Inconclusive);
        Assert.False(evaluation.PolicyFailed);
    }

    [Fact]
    public void SharedPolicyEvaluationTreatsUnresolvedBaselineAsInconclusiveWithoutUnknownCoverage()
    {
        using var repository = Repository.Create(ForbiddenReference);
        var originalManifest = repository.Manifest();
        var baseline = DriftEngine.Snapshot(
            DriftEngine.Analyze(originalManifest, null, null, null, Now, CancellationToken.None),
            Now);
        var emptyManifest = new FitnessManifest(repository.Root, ".adrguard-fitness.json", []);
        var analysis = DriftEngine.Analyze(emptyManifest, null, baseline, null, Now, CancellationToken.None);

        var evaluation = DriftPolicyEvaluator.Evaluate(analysis, emptyManifest, DriftPolicy.Enforce);

        Assert.Equal(0, analysis.Coverage.Unknown);
        Assert.Equal("unresolved-baseline", Assert.Single(analysis.Findings).BaselineState);
        Assert.True(evaluation.Inconclusive);
        Assert.False(evaluation.PolicyFailed);
    }

    [Fact]
    public void SnapshotWriteLoadRequiresExplicitUpdateAndRejectsMalformedContract()
    {
        using var repository = Repository.Create(ForbiddenReference);
        var snapshot = DriftEngine.Snapshot(DriftEngine.Analyze(repository.Manifest(), null, null, "abc123", Now, CancellationToken.None), Now);
        DriftSnapshotService.Write(repository.Root, "baseline.json", snapshot, update: false);
        Assert.Equal(DriftSnapshotService.Serialize(snapshot), DriftSnapshotService.Serialize(DriftSnapshotService.Load(repository.Root, "baseline.json")));
        Assert.Throws<InvalidOperationException>(() => DriftSnapshotService.Write(repository.Root, "baseline.json", snapshot, update: false));
        DriftSnapshotService.Write(repository.Root, "baseline.json", snapshot, update: true);

        File.WriteAllText(Path.Combine(repository.Root, "bad.json"), "{\"schemaVersion\":\"2.0\",\"repository\":{},\"createdAt\":\"2026-01-01T00:00:00Z\",\"findings\":[]}");
        Assert.Throws<InvalidDataException>(() => DriftSnapshotService.Load(repository.Root, "bad.json"));
        File.WriteAllText(Path.Combine(repository.Root, "missing-fingerprint.json"), "{\"schemaVersion\":\"1.0\",\"repository\":{\"rulesManifest\":\".adrguard-fitness.json\",\"commit\":null},\"createdAt\":\"2026-01-01T00:00:00Z\",\"findings\":[{\"ruleId\":\"domain-no-infrastructure\",\"adrStableId\":\"ADR-1\",\"outcome\":\"violation\",\"checkType\":\"forbid-project-reference\",\"from\":null,\"to\":null,\"confidence\":\"deterministic-static\"}]}");
        Assert.Throws<InvalidDataException>(() => DriftSnapshotService.Load(repository.Root, "missing-fingerprint.json"));
        var serialized = DriftSnapshotService.Serialize(snapshot);
        File.WriteAllText(Path.Combine(repository.Root, "bad-check-type.json"), serialized.Replace("\"checkType\": \"forbid-project-reference\"", "\"checkType\": \"arbitrary\""));
        Assert.Throws<InvalidDataException>(() => DriftSnapshotService.Load(repository.Root, "bad-check-type.json"));
        File.WriteAllText(Path.Combine(repository.Root, "missing-confidence.json"), serialized.Replace(",\n      \"confidence\": \"deterministic-static\"", string.Empty));
        Assert.Throws<InvalidDataException>(() => DriftSnapshotService.Load(repository.Root, "missing-confidence.json"));
        Assert.Throws<InvalidDataException>(() => DriftSnapshotService.Write(repository.Root, "../outside.json", snapshot, false));
    }

    private const string ForbiddenReference = "<Project><ItemGroup><ProjectReference Include=\"../Infrastructure/Infrastructure.csproj\" /></ItemGroup></Project>";

    private sealed class Repository : IDisposable
    {
        private Repository(string root) => Root = root;
        internal string Root { get; }
        internal static Repository Create(string xml)
        {
            var root = Path.Combine(Path.GetTempPath(), $"adr-guard-drift-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(root, "src", "Domain"));
            Directory.CreateDirectory(Path.Combine(root, "src", "Infrastructure"));
            File.WriteAllText(Path.Combine(root, "src", "Infrastructure", "Infrastructure.csproj"), "<Project />");
            var repository = new Repository(root); repository.WriteDomain(xml); return repository;
        }
        internal void WriteDomain(string xml) => File.WriteAllText(Path.Combine(Root, "src", "Domain", "Domain.csproj"), xml);
        internal FitnessManifest Manifest(string to = "src/Infrastructure/Infrastructure.csproj", bool gate = true)
        {
            var rule = new FitnessRule("domain-no-infrastructure", new("ADR-1", "docs/adr/0001.md", FitnessDecisionState.Effective, "Accepted", null),
                ["src/Domain/Domain.csproj", to], FitnessCheckType.ForbidProjectReference,
                "src/Domain/Domain.csproj", to, null, "dotnet-project-reference", "error", gate);
            return new(Root, ".adrguard-fitness.json", [rule]);
        }
        public void Dispose() => Directory.Delete(Root, true);
    }
}
