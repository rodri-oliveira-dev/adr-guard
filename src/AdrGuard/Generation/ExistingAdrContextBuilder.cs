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
            .ThenBy(
                document => document.FileName,
                StringComparer.Ordinal)
            .ToArray();

        var builder = new StringBuilder();
        var includedCount = 0;

        foreach (var document in orderedDocuments)
        {
            var entry = BuildEntry(document);

            var separatorLength = builder.Length == 0
                ? 0
                : AdrGenerationText.DoubleNewLine.Length;

            if (builder.Length
                + separatorLength
                + entry.Length
                > MaximumContextCharacters)
            {
                break;
            }

            if (builder.Length > 0)
            {
                builder.Append(
                    AdrGenerationText.DoubleNewLine);
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

    private static string BuildEntry(
        AdrDocument document)
    {
        var builder = new StringBuilder();

        var id = document.Id is { } value
            ? value.ToString(
                "D4",
                CultureInfo.InvariantCulture)
            : "unknown";

        builder
            .Append("ADR ")
            .Append(id)
            .Append(AdrGenerationText.NewLine)
            .Append("Title: ")
            .Append(
                AdrGenerationText.NormalizeNewLines(
                    document.Title?.Trim()
                    ?? string.Empty))
            .Append(AdrGenerationText.NewLine)
            .Append("Status: ")
            .Append(
                AdrGenerationText.NormalizeNewLines(
                    document.Status?.Trim()
                    ?? string.Empty))
            .Append(AdrGenerationText.NewLine)
            .Append("Decision:")
            .Append(AdrGenerationText.NewLine);

        var decision = document.Sections
            .FirstOrDefault(section =>
                section.Level == 2
                && string.Equals(
                    section.Heading,
                    "Decision",
                    StringComparison.OrdinalIgnoreCase))
            ?.Content
            .Trim();

        builder
            .Append(
                AdrGenerationText.NormalizeNewLines(
                    decision ?? string.Empty))
            .Append(AdrGenerationText.NewLine)
            .Append("Relationships:")
            .Append(AdrGenerationText.NewLine);

        var relationships = AdrReference
            .FindAll(document)
            .Select(reference =>
                Path.GetFileName(
                    reference.ResolvedPath))
            .Where(target =>
                !string.IsNullOrWhiteSpace(target))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(
                target => target,
                StringComparer.Ordinal)
            .ToArray();

        if (relationships.Length == 0)
        {
            builder.Append("none");
            return builder.ToString();
        }

        for (var index = 0;
             index < relationships.Length;
             index++)
        {
            if (index > 0)
            {
                builder.Append(
                    AdrGenerationText.NewLine);
            }

            builder
                .Append("- ")
                .Append(relationships[index]);
        }

        return builder.ToString();
    }
}
