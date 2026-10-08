using AdrGuard.Model;
using AdrGuard.Validation;
using System.Text;
using System.Text.Json;

namespace AdrGuard.Cli;

internal static class CheckReportRenderer
{
    internal static void WriteJson(
        IReadOnlyList<AdrDocument> documents,
        ValidationResult result,
        string checkedDirectory,
        TextWriter output) =>
        WriteDocument(output, writer =>
        {
            // Choose a single relative-path namespace for the entire report.
            var reportRoot = GetReportRoot(checkedDirectory);
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", "1.0");
            writer.WriteBoolean("valid", result.IsValid);
            writer.WriteStartObject("summary");
            writer.WriteNumber("files", documents.Count);
            writer.WriteNumber("diagnostics", result.Issues.Count);
            writer.WriteEndObject();
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
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });

    internal static void WriteSarif(
        ValidationResult result,
        string checkedDirectory,
        TextWriter output) =>
        WriteDocument(output, writer =>
        {
            // Choose a single relative-path namespace for the entire report.
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
            writer.WriteStartArray("results");
            foreach (var issue in result.Issues)
            {
                writer.WriteStartObject();
                writer.WriteString("ruleId", issue.Code);
                writer.WriteString("level", "error");
                writer.WriteStartObject("message");
                writer.WriteString("text", issue.Message);
                writer.WriteEndObject();
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

        // Prefer workspace-relative paths; use the checked directory when it
        // is outside (or above) the invocation directory.
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
