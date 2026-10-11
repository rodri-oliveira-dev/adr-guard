namespace AdrGuard.Drift;

internal sealed record DriftSnapshot(
    string SchemaVersion,
    DriftSnapshotRepository Repository,
    DateTimeOffset CreatedAt,
    IReadOnlyList<DriftSnapshotFinding> Findings)
{
    internal const string CurrentSchemaVersion = "1.0";
}
internal sealed record DriftSnapshotRepository(string RulesManifest, string? Commit);
internal sealed record DriftSnapshotFinding(string Fingerprint, string RuleId, string AdrStableId, string Outcome, string CheckType, string? From, string? To, string Confidence);
internal sealed record DriftFinding(string Fingerprint, string RuleId, string AdrStableId, FitnessOutcome Outcome, string CheckType, string Confidence, string BaselineState, string Explanation, IReadOnlyList<FitnessEvidence> Evidence);
internal sealed record DriftCoverage(int TotalRules, int Pass, int Violations, int Unknown, int NotApplicable);
internal sealed record DriftAnalysis(string SchemaVersion, string RulesManifest, string? Commit, DriftCoverage Coverage, IReadOnlyList<DriftFinding> Findings);
