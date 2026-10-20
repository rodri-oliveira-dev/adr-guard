namespace AdrGuard.Impact;

internal sealed record ImpactReport(
    string SchemaVersion,
    bool Advisory,
    ImpactReportRepository Repository,
    ImpactReportSummary Summary,
    ImpactReportCoverage Coverage,
    ImpactReportUncertainty Uncertainty,
    IReadOnlyList<ImpactReportChange> ChangedFiles,
    IReadOnlyList<ImpactReportDecision> Decisions);

internal sealed record ImpactReportRepository(
    string BaseReference,
    string MergeBase,
    string ManifestPath);

internal sealed record ImpactReportSummary(
    int TotalChanges,
    int AffectedDecisions,
    int UnknownDecisions,
    int NotMatchedDecisions);

internal sealed record ImpactReportCoverage(
    int AffectedChanges,
    int UnknownChanges,
    int NotMatchedChanges);

internal sealed record ImpactReportUncertainty(
    bool HasUnknowns,
    IReadOnlyList<string> DecisionStableIds,
    string Explanation);

internal sealed record ImpactReportChange(
    string Scope,
    string ChangeType,
    string? OldPath,
    string? NewPath,
    int? Similarity,
    string Status,
    IReadOnlyList<string> DecisionStableIds);

internal sealed record ImpactReportDecision(
    string StableId,
    string Path,
    string? Title,
    string? LifecycleStatus,
    string Resolution,
    string Status,
    string? Uncertainty,
    IReadOnlyList<ImpactReportEvidence> Evidence);

internal sealed record ImpactReportEvidence(
    string Scope,
    string ChangeType,
    string PathRole,
    string Path,
    string Pattern,
    string Relationship,
    string Reason);

internal static class ImpactReportFactory
{
    internal static ImpactReport Create(ImpactAnalysisResult analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        var decisions = analysis.Decisions.Select(decision => new ImpactReportDecision(
            decision.Decision.StableId,
            decision.Decision.Path,
            decision.Resolution.Document?.Title,
            decision.Resolution.Status,
            Format(decision.Resolution.Kind),
            Format(decision.Status),
            decision.Resolution.Kind is ImpactDecisionResolutionKind.Proposed
                or ImpactDecisionResolutionKind.Inactive
                or ImpactDecisionResolutionKind.Unknown
                    ? decision.Resolution.Detail
                    : null,
            decision.Evidence.Select(evidence => new ImpactReportEvidence(
                Format(evidence.Scope),
                Format(evidence.ChangeKind),
                Format(evidence.PathRole),
                evidence.Path,
                evidence.Pattern,
                Format(evidence.Relationship),
                evidence.Reason)).ToArray())).ToArray();
        var unknownIds = decisions
            .Where(decision => decision.Status == "unknown")
            .Select(decision => decision.StableId)
            .ToArray();

        return new ImpactReport(
            "1.0",
            Advisory: true,
            new ImpactReportRepository(
                analysis.BaseReference,
                analysis.MergeBase,
                analysis.ManifestPath),
            new ImpactReportSummary(
                analysis.Coverage.TotalChanges,
                decisions.Count(decision => decision.Status == "affected"),
                decisions.Count(decision => decision.Status == "unknown"),
                decisions.Count(decision => decision.Status == "not-matched")),
            new ImpactReportCoverage(
                analysis.Coverage.AffectedChanges,
                analysis.Coverage.UnknownChanges,
                analysis.Coverage.NotMatchedChanges),
            new ImpactReportUncertainty(
                unknownIds.Length > 0,
                unknownIds,
                "Unknown means a mapped ADR could not be resolved to an active or proposed decision; it is not a compliance result."),
            analysis.Changes.Select(change => new ImpactReportChange(
                Format(change.Change.Scope),
                Format(change.Change.Kind),
                change.Change.OldPath,
                change.Change.NewPath,
                change.Change.Similarity,
                Format(change.Status),
                change.DecisionStableIds)).ToArray(),
            decisions);
    }

    private static string Format<T>(T value) where T : struct, Enum =>
        value.ToString() switch
        {
            "NotMatched" => "not-matched",
            "TypeChanged" => "type-changed",
            "DependsOn" => "depends-on",
            _ => value.ToString().ToLowerInvariant(),
        };
}
