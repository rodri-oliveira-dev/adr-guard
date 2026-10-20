namespace AdrGuard.Drift;

internal enum FitnessOutcome { Pass, Violation, Unknown, NotApplicable }
internal enum FitnessCheckType { ForbidProjectReference, ExternalTest }
internal enum FitnessDecisionState { Effective, Inactive, Unknown }

internal sealed record FitnessDecision(string StableId, string Path, FitnessDecisionState State, string? Status, string? Detail);
internal sealed record FitnessRule(
    string Id,
    FitnessDecision Decision,
    IReadOnlyList<string> Projects,
    FitnessCheckType CheckType,
    string? From,
    string? To,
    string? TestId,
    string EvidenceSource,
    string Severity,
    bool Gate);
internal sealed record FitnessManifest(string RepositoryRoot, string ManifestPath, IReadOnlyList<FitnessRule> Rules)
{
    internal const string SchemaVersion = "1.0";
}

internal sealed record FitnessEvidence(
    string Kind,
    string Source,
    string? Subject,
    string? Target,
    int? Line,
    string Detail);

internal sealed record FitnessRuleResult(
    string RuleId,
    string AdrStableId,
    FitnessOutcome Outcome,
    string Confidence,
    string Explanation,
    IReadOnlyList<FitnessEvidence> Evidence);

internal sealed record ExternalEvidenceProvenance(string Producer, string ProducerVersion, string RunId, string ArtifactId, string Sha256, string EvidencePath);
internal sealed record ExternalObservation(string TestId, string Status, DateTimeOffset? ObservedAt, ExternalEvidenceProvenance Provenance);
internal sealed record ExternalEvidenceSet(IReadOnlyList<ExternalObservation> Observations);

internal sealed class FitnessManifestException : Exception
{
    internal FitnessManifestException(string message) : base(message) { }
    internal FitnessManifestException(string message, Exception inner) : base(message, inner) { }
}
