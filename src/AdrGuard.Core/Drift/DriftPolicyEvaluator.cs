namespace AdrGuard.Drift;

internal enum DriftPolicy { Advisory, Enforce }

internal sealed record DriftReport(
    string SchemaVersion,
    string Policy,
    DriftReportRepository Repository,
    DriftCoverage Coverage,
    IReadOnlyList<DriftReportFinding> Findings);

internal sealed record DriftReportRepository(string RulesManifest, string? Commit);

internal sealed record DriftReportFinding(
    string Fingerprint,
    string RuleId,
    string AdrStableId,
    string Outcome,
    string CheckType,
    string Confidence,
    string BaselineState,
    string Explanation,
    IReadOnlyList<FitnessEvidence> Evidence);

internal sealed record DriftPolicyEvaluation(
    DriftReport Report,
    bool Inconclusive,
    bool PolicyFailed);

internal static class DriftPolicyEvaluator
{
    internal static DriftPolicyEvaluation Evaluate(
        DriftAnalysis analysis,
        FitnessManifest manifest,
        DriftPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(manifest);

        var report = new DriftReport(
            "1.0",
            policy.ToString().ToLowerInvariant(),
            new DriftReportRepository(analysis.RulesManifest, analysis.Commit),
            analysis.Coverage,
            analysis.Findings.Select(item => new DriftReportFinding(
                item.Fingerprint,
                item.RuleId,
                item.AdrStableId,
                DriftEngine.Format(item.Outcome),
                item.CheckType,
                item.Confidence,
                item.BaselineState,
                item.Explanation,
                item.Evidence)).ToArray());
        var inconclusive = analysis.Coverage.Unknown > 0
                           || analysis.Findings.Any(item => item.BaselineState == "unresolved-baseline");
        var rules = manifest.Rules.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var policyFailed = policy == DriftPolicy.Enforce
                           && analysis.Findings.Any(item => item.Outcome == FitnessOutcome.Violation
                               && item.Confidence == "deterministic-static"
                               && item.BaselineState == "new"
                               && rules.TryGetValue(item.RuleId, out var rule)
                               && rule.Gate);

        return new DriftPolicyEvaluation(report, inconclusive, policyFailed);
    }
}
