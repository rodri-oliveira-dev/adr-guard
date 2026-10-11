using System.Text.Json;
using AdrGuard.Drift;
using AdrGuard.Git;

namespace AdrGuard.Cli;

internal enum DriftOutputFormat { Text, Json }
internal enum DriftPolicy { Advisory, Enforce }

internal static class DriftCommand
{
    internal static int Run(string repositoryPath, string rulesPath, IReadOnlyList<string> evidencePaths, string? baselinePath,
        string? writeBaselinePath, bool updateBaseline, DriftOutputFormat format, DriftPolicy policy,
        TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        try
        {
            var root = Path.GetFullPath(repositoryPath);
            var manifest = FitnessManifestLoader.Load(root, rulesPath);
            var evidence = evidencePaths.Count == 0 ? null : ExternalFitnessEvidenceAdapter.Load(root, evidencePaths, cancellationToken);
            var baseline = baselinePath is null ? null : DriftSnapshotService.Load(root, baselinePath);
            var commit = TryReadCommit(root, cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var analysis = DriftEngine.Analyze(manifest, evidence, baseline, commit, now, cancellationToken);
            var report = DriftReportFactory.Create(analysis, policy);
            if (format == DriftOutputFormat.Json) DriftReportRenderer.WriteJson(report, output);
            else DriftReportRenderer.WriteText(report, output);

            if (writeBaselinePath is not null)
                DriftSnapshotService.Write(root, writeBaselinePath, DriftEngine.Snapshot(analysis, now), updateBaseline);

            if (analysis.Coverage.Unknown > 0 || analysis.Findings.Any(x => x.BaselineState == "unresolved-baseline"))
            {
                error.WriteLine("Architecture drift analysis is inconclusive; unknown evidence is not compliance.");
                return ExitCodes.OperationalError;
            }
            var rules = manifest.Rules.ToDictionary(x => x.Id, StringComparer.Ordinal);
            var blocking = analysis.Findings.Any(x => x.Outcome == FitnessOutcome.Violation && x.Confidence == "deterministic-static"
                && x.BaselineState == "new" && rules.TryGetValue(x.RuleId, out var rule) && rule.Gate);
            if (policy == DriftPolicy.Enforce && blocking)
            {
                error.WriteLine("Architecture drift enforcement failed for one or more new deterministic violations.");
                return ExitCodes.PolicyFailed;
            }
            return ExitCodes.Success;
        }
        catch (OperationCanceledException) { error.WriteLine("Architecture drift analysis was canceled."); return ExitCodes.OperationalError; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException
            or FitnessManifestException or AdrGuard.Configuration.AdrGuardConfigurationException)
        { error.WriteLine($"Unable to analyze architecture drift: {exception.Message}"); return ExitCodes.OperationalError; }
    }

    private static string? TryReadCommit(string root, CancellationToken cancellationToken)
    {
        try { return GitCommandRunner.RunBounded(root, ["rev-parse", "--verify", "HEAD"], TimeSpan.FromSeconds(5), 256, cancellationToken).Trim(); }
        catch (GitOperationException) { return null; }
    }
}

internal sealed record DriftReport(string SchemaVersion, string Policy, DriftReportRepository Repository, DriftCoverage Coverage, IReadOnlyList<DriftReportFinding> Findings);
internal sealed record DriftReportRepository(string RulesManifest, string? Commit);
internal sealed record DriftReportFinding(string Fingerprint, string RuleId, string AdrStableId, string Outcome, string CheckType, string Confidence, string BaselineState, string Explanation, IReadOnlyList<FitnessEvidence> Evidence);

internal static class DriftReportFactory
{
    internal static DriftReport Create(DriftAnalysis analysis, DriftPolicy policy) => new("1.0", policy.ToString().ToLowerInvariant(),
        new(analysis.RulesManifest, analysis.Commit), analysis.Coverage,
        analysis.Findings.Select(x => new DriftReportFinding(x.Fingerprint, x.RuleId, x.AdrStableId, DriftEngine.Format(x.Outcome), x.CheckType, x.Confidence, x.BaselineState, x.Explanation, x.Evidence)).ToArray());
}

internal static class DriftReportRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true, NewLine = "\n" };
    internal static void WriteJson(DriftReport report, TextWriter output) { output.Write(JsonSerializer.Serialize(report, JsonOptions)); output.Write('\n'); }
    internal static void WriteText(DriftReport report, TextWriter output)
    {
        output.WriteLine($"Architecture drift ({report.Policy})");
        output.WriteLine($"Rules: {report.Coverage.TotalRules}; pass: {report.Coverage.Pass}; violations: {report.Coverage.Violations}; unknown: {report.Coverage.Unknown}; not applicable: {report.Coverage.NotApplicable}");
        foreach (var finding in report.Findings)
        {
            output.WriteLine();
            output.WriteLine($"[{finding.Outcome}/{finding.BaselineState}] {finding.AdrStableId} {finding.RuleId} ({finding.Confidence})");
            output.WriteLine($"  {finding.Explanation}");
            foreach (var evidence in finding.Evidence) output.WriteLine($"  evidence: {evidence.Kind} {evidence.Subject ?? "-"} -> {evidence.Target ?? "-"}{(evidence.Line is null ? "" : $" line {evidence.Line}")}");
        }
    }
}
