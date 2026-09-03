using AdrGuard.Model;
using AdrGuard.Validation;
using System.Globalization;
using System.Text;

namespace AdrGuard.Generation;

internal sealed record ExistingAdrContext(
    string Content,
    int IncludedCount,
    int TotalCount,
    bool IsBounded);

internal static class ExistingAdrContextBuilder
{
    internal const int MaximumContextCharacters = 12000;

    internal static ExistingAdrContext Build(
        IReadOnlyList<AdrDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var orderedDocuments = documents
            .OrderBy(document => document.Id ?? int.MaxValue)
            .ThenBy(document => document.FileName, StringComparer.Ordinal)
            .ToArray();

        var builder = new StringBuilder();
        var includedCount = 0;

        foreach (var document in orderedDocuments)
        {
            var entry = BuildEntry(document);

            var separatorLength = builder.Length == 0
                ? 0
                : Environment.NewLine.Length * 2;

            if (builder.Length + separatorLength + entry.Length
                > MaximumContextCharacters)
            {
                break;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine();
            }

            builder.Append(entry);
            includedCount++;
        }

        return new ExistingAdrContext(
            builder.ToString(),
            includedCount,
            orderedDocuments.Length,
            includedCount < orderedDocuments.Length);
    }

    private static string BuildEntry(AdrDocument document)
    {
        var builder = new StringBuilder();

        var id = document.Id is { } value
            ? value.ToString("D4", CultureInfo.InvariantCulture)
            : "unknown";

        builder
            .Append("ADR ")
            .AppendLine(id)
            .Append("Title: ")
            .AppendLine(document.Title?.Trim() ?? string.Empty)
            .Append("Status: ")
            .AppendLine(document.Status?.Trim() ?? string.Empty)
            .AppendLine("Decision:");

        var decision = document.Sections
            .FirstOrDefault(section =>
                string.Equals(
                    section.Heading,
                    "Decision",
                    StringComparison.OrdinalIgnoreCase))
            ?.Content
            .Trim();

        builder.AppendLine(decision ?? string.Empty);

        var relationships = AdrReference
            .FindAll(document)
            .Select(reference => Path.GetFileName(reference.ResolvedPath))
            .Where(target => !string.IsNullOrWhiteSpace(target))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(target => target, StringComparer.Ordinal)
            .ToArray();

        builder.AppendLine("Relationships:");

        if (relationships.Length == 0)
        {
            builder.Append("none");
            return builder.ToString();
        }

        for (var index = 0; index < relationships.Length; index++)
        {
            if (index > 0)
            {
                builder.AppendLine();
            }

            builder
                .Append("- ")
                .Append(relationships[index]);
        }

        return builder.ToString();
    }
}
