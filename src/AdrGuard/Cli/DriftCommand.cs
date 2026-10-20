using System.Text.Json;
using AdrGuard.Drift;
using AdrGuard.Git;

namespace AdrGuard.Cli;

internal enum DriftOutputFormat { Text, Json }

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
            var evaluation = DriftPolicyEvaluator.Evaluate(analysis, manifest, policy);
            var report = evaluation.Report;
            if (format == DriftOutputFormat.Json) DriftReportRenderer.WriteJson(report, output);
            else DriftReportRenderer.WriteText(report, output);

            if (writeBaselinePath is not null)
                DriftSnapshotService.Write(root, writeBaselinePath, DriftEngine.Snapshot(analysis, now), updateBaseline);

            if (evaluation.Inconclusive)
            {
                error.WriteLine("Architecture drift analysis is inconclusive; unknown evidence is not compliance.");
                return ExitCodes.OperationalError;
            }
            if (evaluation.PolicyFailed)
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
