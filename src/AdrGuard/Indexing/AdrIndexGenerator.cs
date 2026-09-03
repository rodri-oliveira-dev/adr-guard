using AdrGuard.Model;
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

    private static string EscapeTableCell(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal);
}
