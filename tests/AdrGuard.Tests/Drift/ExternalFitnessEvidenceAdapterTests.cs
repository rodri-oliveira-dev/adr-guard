using System.Globalization;
using AdrGuard.Drift;
using Xunit;

namespace AdrGuard.Tests.Drift;

public sealed class ExternalFitnessEvidenceAdapterTests
{
    [Fact]
    public void LoadsFixtureAndPreservesReportedProvenance()
    {
        var root = FixtureRoot;
        var manifest = FitnessManifestLoader.Load(root, ".adrguard-fitness.json");
        var evidence = ExternalFitnessEvidenceAdapter.Load(root, ["external-evidence.json"], CancellationToken.None);
        var result = Assert.Single(ExternalFitnessEvidenceAdapter.Evaluate(manifest, evidence, At("2026-10-12T12:00:00Z")));
        Assert.Equal(FitnessOutcome.Pass, result.Outcome);
        Assert.Equal("reported-external", result.Confidence);
        Assert.Contains("ci-1842", Assert.Single(result.Evidence).Detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("skipped", "Unknown")]
    [InlineData("error", "Unknown")]
    [InlineData("failed", "Violation")]
    public void MapsReportedStatusesWithoutPromotingTrust(string status, string expected)
    {
        using var repository = EvidenceRepository.Create(status, "2026-10-11T12:00:00Z");
        var result = repository.Evaluate(At("2026-10-12T12:00:00Z"));
        Assert.Equal(expected, result.Outcome.ToString());
        Assert.NotEqual("deterministic-static", result.Confidence);
    }

    [Fact]
    public void MissingStaleAndContradictoryEvidenceAreUnknown()
    {
        using var repository = EvidenceRepository.Create("passed", "2025-01-01T00:00:00Z");
        Assert.Equal(FitnessOutcome.Unknown, repository.Evaluate(At("2026-10-12T12:00:00Z")).Outcome);

        var manifest = repository.Manifest("Missing.Test");
        var evidence = ExternalFitnessEvidenceAdapter.Load(repository.Root, ["evidence.json"], CancellationToken.None);
        Assert.Equal(FitnessOutcome.Unknown, Assert.Single(ExternalFitnessEvidenceAdapter.Evaluate(manifest, evidence, At("2026-10-12T12:00:00Z"))).Outcome);

        File.WriteAllText(Path.Combine(repository.Root, "conflict.json"), EvidenceRepository.Json("failed", "2026-10-11T12:00:00Z", "run-2"));
        evidence = ExternalFitnessEvidenceAdapter.Load(repository.Root, ["evidence.json", "conflict.json"], CancellationToken.None);
        Assert.Equal("conflicting-external", Assert.Single(ExternalFitnessEvidenceAdapter.Evaluate(repository.Manifest(), evidence, At("2026-10-12T12:00:00Z"))).Confidence);
    }

    [Fact]
    public void RejectsUnknownFieldsDuplicatesAndRemotePaths()
    {
        using var repository = EvidenceRepository.Create("passed", null);
        Assert.Throws<FitnessManifestException>(() => ExternalFitnessEvidenceAdapter.Load(repository.Root, ["https://example.test/evidence.json"], CancellationToken.None));

        File.WriteAllText(Path.Combine(repository.Root, "bad.json"), EvidenceRepository.Json("passed", null).Replace("\"schemaVersion\": \"1.0\"", "\"schemaVersion\": \"1.0\", \"unexpected\": true"));
        Assert.Throws<FitnessManifestException>(() => ExternalFitnessEvidenceAdapter.Load(repository.Root, ["bad.json"], CancellationToken.None));

        File.WriteAllText(Path.Combine(repository.Root, "duplicate.json"), EvidenceRepository.Json("passed", null).Replace("\"status\": \"passed\"", "\"status\": \"passed\", \"status\": \"failed\""));
        Assert.Throws<FitnessManifestException>(() => ExternalFitnessEvidenceAdapter.Load(repository.Root, ["duplicate.json"], CancellationToken.None));
    }

    [Fact]
    public void HonorsCancellation()
    {
        using var repository = EvidenceRepository.Create("passed", null);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => ExternalFitnessEvidenceAdapter.Load(repository.Root, ["evidence.json"], cancellation.Token));
    }

    private static string FixtureRoot => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Drift", "contract");
    private static DateTimeOffset At(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private sealed class EvidenceRepository : IDisposable
    {
        private EvidenceRepository(string root) => Root = root;
        internal string Root { get; }
        internal static EvidenceRepository Create(string status, string? timestamp)
        {
            var root = Path.Combine(Path.GetTempPath(), $"adr-guard-external-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "evidence.json"), Json(status, timestamp));
            return new(root);
        }
        internal FitnessManifest Manifest(string testId = "Architecture.DomainBoundary")
        {
            var rule = new FitnessRule("architecture-tests", new("ADR-1", "docs/adr/0001.md", FitnessDecisionState.Effective, "Accepted", null), ["src/Domain/Domain.csproj"], FitnessCheckType.ExternalTest, null, null, testId, "external-test-report", "warning", false);
            return new(Root, ".adrguard-fitness.json", [rule]);
        }
        internal FitnessRuleResult Evaluate(DateTimeOffset now)
        {
            var evidence = ExternalFitnessEvidenceAdapter.Load(Root, ["evidence.json"], CancellationToken.None);
            return Assert.Single(ExternalFitnessEvidenceAdapter.Evaluate(Manifest(), evidence, now));
        }
        internal static string Json(string status, string? timestamp, string run = "run-1") => $$"""
            {
              "schemaVersion": "1.0",
              "producer": { "name": "tests", "version": "1.0" },
              "artifact": { "runId": "{{run}}", "id": "results", "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef" },
              "observations": [{ "testId": "Architecture.DomainBoundary", "status": "{{status}}"{{(timestamp is null ? "" : $", \"observedAt\": \"{timestamp}\"")}} }]
            }
            """;
        public void Dispose() => Directory.Delete(Root, true);
    }
}
