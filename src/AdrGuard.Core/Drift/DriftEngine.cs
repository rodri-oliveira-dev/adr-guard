using System.Security.Cryptography;
using System.Text;

namespace AdrGuard.Drift;

internal static class DriftEngine
{
    internal static DriftAnalysis Analyze(FitnessManifest manifest, ExternalEvidenceSet? externalEvidence, DriftSnapshot? baseline, string? commit, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var results = DotNetProjectReferenceChecker.Check(manifest, cancellationToken).ToList();
        if (externalEvidence is null)
        {
            results.AddRange(manifest.Rules.Where(x => x.CheckType == FitnessCheckType.ExternalTest).Select(rule =>
                new FitnessRuleResult(rule.Id, rule.Decision.StableId,
                    rule.Decision.State == FitnessDecisionState.Inactive ? FitnessOutcome.NotApplicable : FitnessOutcome.Unknown,
                    "none", rule.Decision.State == FitnessDecisionState.Inactive ? rule.Decision.Detail! : "No external evidence file was supplied.", [])));
        }
        else results.AddRange(ExternalFitnessEvidenceAdapter.Evaluate(manifest, externalEvidence, now));

        var baselineByFingerprint = baseline?.Findings.ToDictionary(x => x.Fingerprint, StringComparer.Ordinal)
            ?? new Dictionary<string, DriftSnapshotFinding>(StringComparer.Ordinal);
        var rulesById = manifest.Rules.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var findings = results.Select(result =>
        {
            var rule = rulesById[result.RuleId];
            var fingerprint = Fingerprint(rule, result.Outcome);
            var state = result.Outcome == FitnessOutcome.Violation
                ? baselineByFingerprint.ContainsKey(fingerprint) ? "existing" : "new"
                : "current";
            return new DriftFinding(fingerprint, result.RuleId, result.AdrStableId, result.Outcome, Format(rule.CheckType), result.Confidence, state, result.Explanation, result.Evidence);
        }).ToList();

        if (baseline is not null)
        {
            foreach (var previous in baseline.Findings.Where(x => x.Outcome == "violation"))
            {
                if (findings.Any(x => x.Fingerprint == previous.Fingerprint && x.Outcome == FitnessOutcome.Violation)) continue;
                var state = rulesById.TryGetValue(previous.RuleId, out var currentRule)
                    && findings.Any(x => x.RuleId == previous.RuleId && x.Outcome == FitnessOutcome.Pass)
                    && Fingerprint(currentRule, FitnessOutcome.Violation) == previous.Fingerprint
                    ? "resolved"
                    : "unresolved-baseline";
                findings.Add(new DriftFinding(previous.Fingerprint, previous.RuleId, previous.AdrStableId, FitnessOutcome.Violation, previous.CheckType, previous.Confidence, state,
                    state == "resolved" ? "The previously recorded violation is now disproved by complete passing evidence." : "The baseline violation cannot be resolved because current evidence is missing, inconclusive, non-applicable, or the mapping was removed.", []));
            }
        }

        var currentFindings = findings.Where(x => x.BaselineState is not ("resolved" or "unresolved-baseline")).ToArray();
        var coverage = new DriftCoverage(currentFindings.Length,
            currentFindings.Count(x => x.Outcome == FitnessOutcome.Pass), currentFindings.Count(x => x.Outcome == FitnessOutcome.Violation),
            currentFindings.Count(x => x.Outcome == FitnessOutcome.Unknown), currentFindings.Count(x => x.Outcome == FitnessOutcome.NotApplicable));
        return new DriftAnalysis("1.0", manifest.ManifestPath, commit, coverage,
            findings.OrderBy(x => x.AdrStableId, StringComparer.Ordinal).ThenBy(x => x.RuleId, StringComparer.Ordinal).ThenBy(x => x.BaselineState, StringComparer.Ordinal).ThenBy(x => x.Fingerprint, StringComparer.Ordinal).ToArray());
    }

    internal static DriftSnapshot Snapshot(DriftAnalysis analysis, DateTimeOffset createdAt) => new(
        DriftSnapshot.CurrentSchemaVersion, new(analysis.RulesManifest, analysis.Commit), createdAt,
        analysis.Findings.Where(x => x.BaselineState is not ("resolved" or "unresolved-baseline")).Select(finding =>
        {
            var parts = finding.Evidence.Count == 0 ? null : finding.Evidence[0];
            return new DriftSnapshotFinding(finding.Fingerprint, finding.RuleId, finding.AdrStableId, Format(finding.Outcome), finding.CheckType, parts?.Subject, parts?.Target, finding.Confidence);
        }).OrderBy(x => x.AdrStableId, StringComparer.Ordinal).ThenBy(x => x.RuleId, StringComparer.Ordinal).ThenBy(x => x.Fingerprint, StringComparer.Ordinal).ToArray());

    internal static string Fingerprint(FitnessRule rule, FitnessOutcome outcome)
    {
        var material = string.Join('\n', FitnessManifest.SchemaVersion, rule.Decision.StableId, rule.Id, Format(rule.CheckType),
            string.Join('\n', rule.Projects.Order(StringComparer.Ordinal)), rule.From ?? string.Empty, rule.To ?? string.Empty, rule.TestId ?? string.Empty, Format(outcome));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
    }
    internal static string Format<T>(T value) where T : struct, Enum => value.ToString() switch
    {
        "NotApplicable" => "not-applicable",
        "ForbidProjectReference" => "forbid-project-reference",
        "ExternalTest" => "external-test",
        _ => value.ToString().ToLowerInvariant(),
    };
}
