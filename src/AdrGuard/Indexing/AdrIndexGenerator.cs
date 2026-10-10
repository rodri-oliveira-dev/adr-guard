using AdrGuard.Model;
using AdrGuard.Validation;
using System.Globalization;
using System.Text;

namespace AdrGuard.Indexing;

internal static class AdrIndexGenerator
{
    internal static string Generate(IReadOnlyList<AdrDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var builder = new StringBuilder();

        builder.AppendLine("# Architecture Decision Records");
        builder.AppendLine();
        builder.AppendLine("| ADR | Decision | Status |");
        builder.AppendLine("| --- | --- | --- |");

        foreach (var document in documents
                     .OrderBy(document => document.Id)
                     .ThenBy(document => document.FileName, StringComparer.Ordinal))
        {
            var id = document.Id?.ToString("D4", CultureInfo.InvariantCulture) ?? "----";
            var title = EscapeTableCell(document.Title ?? "(untitled)");
            var status = EscapeTableCell(document.Status ?? "(missing)");
            var fileName = Uri.EscapeDataString(document.FileName)
                .Replace("%2F", "/", StringComparison.OrdinalIgnoreCase);

            builder.Append("| [")
                .Append(id)
                .Append("](")
                .Append(fileName)
                .Append(") | ")
                .Append(title)
                .Append(" | ")
                .Append(status)
                .AppendLine(" |");
        }

        return builder.ToString();
    }

    internal static string GenerateEnriched(IReadOnlyList<AdrDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var builder = new StringBuilder();
        builder.AppendLine("# ADR governance catalog");
        builder.AppendLine();
        builder.AppendLine("| ADR | Decision | Status | Date | Owner | Category | Successor | Last reviewed |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var document in documents.OrderBy(item => item.Id).ThenBy(item => item.FileName, StringComparer.Ordinal))
        {
            var metadata = document.DecisionMetadata;
            var sectionRelations = AdrReference.FindInSections(document, "Superseded by");
            var statusRelations = AdrReference.FindInStatusText(document);
            var successor = sectionRelations.Count > 0
                ? sectionRelations[0].Target
                : statusRelations.Count > 0 ? statusRelations[0].Target : "unknown";
            var id = document.Id?.ToString("D4", CultureInfo.InvariantCulture)
                ?? EscapeTableCell(document.StableId ?? Path.GetFileNameWithoutExtension(document.FileName));
            var fileName = Uri.EscapeDataString(document.FileName).Replace("%2F", "/", StringComparison.OrdinalIgnoreCase);
            builder.Append("| [").Append(id).Append("](").Append(fileName).Append(") | ")
                .Append(EscapeTableCell(document.Title ?? "unknown")).Append(" | ")
                .Append(EscapeTableCell(document.Status ?? "unknown")).Append(" | ")
                .Append(EscapeTableCell(metadata?.DecisionDate ?? "unknown")).Append(" | ")
                .Append(EscapeTableCell(metadata?.Owner ?? "unknown")).Append(" | ")
                .Append(EscapeTableCell(metadata?.Category ?? "unknown")).Append(" | ")
                .Append(EscapeTableCell(successor)).Append(" | ")
                .Append(EscapeTableCell(metadata?.LastReviewedDate ?? "unknown")).AppendLine(" |");
        }
        return builder.ToString();
    }

    private static string EscapeTableCell(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal);
}
