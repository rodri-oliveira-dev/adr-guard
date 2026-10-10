using AdrGuard.Model;
using AdrGuard.Validation;
using AdrGuard.Baselines;
using System.Text;
using System.Text.Json;

namespace AdrGuard.Cli;

internal static class CheckReportRenderer
{
    internal static void WriteJson(
        IReadOnlyList<AdrDocument> documents,
        ValidationResult result,
        string checkedDirectory,
        DiagnosticBaselineComparison? baseline,
        TextWriter output) =>
        WriteDocument(output, writer =>
        {
            // All report paths use one root for stable JSON/SARIF identity.
            var reportRoot = GetReportRoot(checkedDirectory);
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", "1.0");
            writer.WriteBoolean(
                "valid",
                baseline is null ? result.IsValid : new ValidationResult(baseline.NewIssues).IsValid);
            writer.WriteStartObject("summary");
            writer.WriteNumber("files", documents.Count);
            writer.WriteNumber("diagnostics", result.Issues.Count);
            writer.WriteEndObject();
            WriteBaselineSummary(writer, baseline);
            writer.WriteStartArray("files");
            foreach (var document in documents.OrderBy(item => item.FilePath, StringComparer.Ordinal))
            {
                writer.WriteStringValue(ToReportPath(document.FilePath, reportRoot));
            }

            writer.WriteEndArray();
            writer.WriteStartArray("diagnostics");
            foreach (var issue in result.Issues)
            {
                writer.WriteStartObject();
                writer.WriteString("code", issue.Code);
                writer.WriteString("message", issue.Message);
                writer.WriteString("file", ToReportPath(issue.FilePath, reportRoot));
                if (issue.Severity == ValidationSeverity.Warning) writer.WriteString("severity", "warning");
                WriteJsonBaselineState(writer, issue, checkedDirectory, baseline);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });

    internal static void WriteSarif(
        ValidationResult result,
        string checkedDirectory,
        DiagnosticBaselineComparison? baseline,
        TextWriter output) =>
        WriteDocument(output, writer =>
        {
            // All report paths use one root for stable JSON/SARIF identity.
            var reportRoot = GetReportRoot(checkedDirectory);
            writer.WriteStartObject();
            writer.WriteString(
                "$schema",
                "https://json.schemastore.org/sarif-2.1.0.json");
            writer.WriteString("version", "2.1.0");
            writer.WriteStartArray("runs");
            writer.WriteStartObject();
            writer.WriteStartObject("tool");
            writer.WriteStartObject("driver");
            writer.WriteString("name", "ADR Guard");
            writer.WriteString(
                "informationUri",
                "https://github.com/rodri-oliveira-dev/adr-guard");
            writer.WriteStartArray("rules");
            foreach (var code in result.Issues
                         .Select(issue => issue.Code)
                         .Distinct(StringComparer.Ordinal)
                         .Order(StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("id", code);
                writer.WriteStartObject("shortDescription");
                writer.WriteString("text", ValidationRuleCatalog.GetDescription(code));
                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();
            if (baseline is not null)
            {
                writer.WriteStartObject("properties");
                WriteBaselineSummary(writer, baseline);
                writer.WriteEndObject();
            }

            writer.WriteStartArray("results");
            foreach (var issue in result.Issues)
            {
                writer.WriteStartObject();
                writer.WriteString("ruleId", issue.Code);
                writer.WriteString("level", issue.Severity == ValidationSeverity.Warning ? "warning" : "error");
                writer.WriteStartObject("message");
                writer.WriteString("text", issue.Message);
                writer.WriteEndObject();
                WriteSarifBaselineState(writer, issue, checkedDirectory, baseline);
                writer.WriteStartArray("locations");
                writer.WriteStartObject();
                writer.WriteStartObject("physicalLocation");
                writer.WriteStartObject("artifactLocation");
                writer.WriteString("uri", ToSarifUri(ToReportPath(issue.FilePath, reportRoot)));
                writer.WriteEndObject();
                writer.WriteEndObject();
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();
        });

    private static void WriteBaselineSummary(
        Utf8JsonWriter writer,
        DiagnosticBaselineComparison? baseline)
    {
        if (baseline is null)
        {
            return;
        }

        writer.WriteStartObject("baseline");
        writer.WriteNumber("new", baseline.NewIssues.Count);
        writer.WriteNumber("existing", baseline.ExistingIssues.Count);
        writer.WriteNumber("resolved", baseline.ResolvedEntries.Count);
        writer.WriteEndObject();
    }

    private static void WriteJsonBaselineState(
        Utf8JsonWriter writer,
        ValidationIssue issue,
        string checkedDirectory,
        DiagnosticBaselineComparison? baseline)
    {
        if (baseline is null)
        {
            return;
        }

        writer.WriteString(
            "baselineState",
            GetBaselineState(issue, checkedDirectory, baseline));
    }

    private static void WriteSarifBaselineState(
        Utf8JsonWriter writer,
        ValidationIssue issue,
        string checkedDirectory,
        DiagnosticBaselineComparison? baseline)
    {
        if (baseline is null)
        {
            return;
        }

        writer.WriteStartObject("properties");
        writer.WriteString(
            "adrGuardBaselineState",
            GetBaselineState(issue, checkedDirectory, baseline));
        writer.WriteEndObject();
    }

    private static string GetBaselineState(
        ValidationIssue issue,
        string checkedDirectory,
        DiagnosticBaselineComparison baseline)
    {
        var fingerprint = DiagnosticBaselineService.Fingerprint(issue, checkedDirectory);
        return baseline.StateByFingerprint.TryGetValue(fingerprint, out var state)
            ? state
            : "new";
    }

    private static void WriteDocument(
        TextWriter output,
        Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
                   stream,
                   new JsonWriterOptions { Indented = true }))
        {
            write(writer);
        }

        output.WriteLine(Encoding.UTF8.GetString(stream.ToArray()));
    }

    private static string GetReportRoot(string checkedDirectory)
    {
        var invocationRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
        var checkedRoot = Path.GetFullPath(checkedDirectory);

        // Use workspace-relative paths when the checked directory belongs to it.
        return IsContained(invocationRoot, checkedRoot)
            ? invocationRoot
            : checkedRoot;
    }

    private static string ToReportPath(string filePath, string reportRoot) =>
        Path.GetRelativePath(reportRoot, Path.GetFullPath(filePath))
            .Replace(Path.DirectorySeparatorChar, '/');

    private static string ToSarifUri(string reportPath) =>
        string.Join("/", reportPath.Split('/').Select(Uri.EscapeDataString));

    private static bool IsContained(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative != ".."
            && !Path.IsPathRooted(relative)
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }
}
