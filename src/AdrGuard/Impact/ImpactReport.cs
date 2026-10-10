using System.Text.Json;

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

internal static class ImpactReportRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        NewLine = "\n",
    };

    internal static void WriteJson(ImpactReport report, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(output);

        output.Write(JsonSerializer.Serialize(report, JsonOptions));
        output.Write('\n');
    }

    internal static void WriteText(ImpactReport report, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(output);

        output.WriteLine("Architecture impact analysis (advisory; not a compliance decision)");
        output.WriteLine($"Base: {report.Repository.BaseReference} ({report.Repository.MergeBase})");
        output.WriteLine($"Changes: {report.Summary.TotalChanges}; affected: {report.Coverage.AffectedChanges}; unknown: {report.Coverage.UnknownChanges}; not matched: {report.Coverage.NotMatchedChanges}");

        foreach (var decision in report.Decisions.Where(item => item.Status != "not-matched"))
        {
            output.WriteLine();
            output.WriteLine($"[{decision.Status}] {decision.StableId} - {decision.Title ?? decision.Path}");
            foreach (var evidence in decision.Evidence)
            {
                output.WriteLine($"  {evidence.Scope}/{evidence.ChangeType}: {evidence.Path} ({evidence.PathRole}) matches {evidence.Pattern}: {evidence.Reason}");
            }

            if (decision.Uncertainty is not null)
            {
                output.WriteLine($"  Uncertainty: {decision.Uncertainty}");
            }
        }

        if (report.ChangedFiles.Count == 0)
        {
            output.WriteLine("No changed files were found in the selected Git scope.");
        }
        else if (report.Coverage.NotMatchedChanges > 0)
        {
            output.WriteLine();
            output.WriteLine($"Coverage note: {report.Coverage.NotMatchedChanges} changed file(s) have no explicit ADR mapping.");
        }
    }
}
