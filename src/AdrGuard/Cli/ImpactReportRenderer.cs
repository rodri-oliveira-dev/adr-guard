using System.Text.Json;
using AdrGuard.Impact;

namespace AdrGuard.Cli;

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
