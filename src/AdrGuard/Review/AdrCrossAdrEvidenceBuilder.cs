using AdrGuard.Model;
using AdrGuard.Validation;
using System.Globalization;
using System.Text;

namespace AdrGuard.Review;

internal sealed record AdrCrossAdrEvidence(
    string Content,
    IReadOnlyList<string> IncludedSourceNames,
    int TotalCandidateCount,
    bool IsBounded);

internal static class AdrCrossAdrEvidenceBuilder
{
    internal const int MaximumCharacters = 16000;

    internal static AdrCrossAdrEvidence? Build(
        AdrDocument target,
        IReadOnlyList<AdrDocument> existingDocuments,
        int maximumCharacters = MaximumCharacters)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(existingDocuments);

        if (existingDocuments.Count == 0
            || maximumCharacters <= 0)
        {
            return null;
        }

        var ordered = existingDocuments
            .OrderBy(document => document.Id ?? int.MaxValue)
            .ThenBy(document => document.FileName, StringComparer.Ordinal)
            .ToArray();

        var builder = new StringBuilder();
        var includedSourceNames = new List<string>();

        builder.Append("Cross-ADR comparison evidence:")
            .Append(Environment.NewLine)
            .Append(BuildDocumentEvidence("Target", target));

        if (builder.Length > maximumCharacters)
        {
            return null;
        }

        foreach (var document in ordered)
        {
            var entry = Environment.NewLine
                + Environment.NewLine
                + BuildDocumentEvidence("Candidate", document);

            if (builder.Length + entry.Length > maximumCharacters)
            {
                break;
            }

            builder.Append(entry);
            includedSourceNames.Add(document.FileName);
        }

        return new AdrCrossAdrEvidence(
            builder.ToString(),
            includedSourceNames,
            ordered.Length,
            includedSourceNames.Count < ordered.Length);
    }

    private static string BuildDocumentEvidence(
        string role,
        AdrDocument document)
    {
        var id = document.Id is { } value
            ? value.ToString("D4", CultureInfo.InvariantCulture)
            : "unknown";

        var decision = document.Sections
            .FirstOrDefault(section =>
                section.Level == 2
                && string.Equals(
                    section.Heading,
                    "Decision",
                    StringComparison.OrdinalIgnoreCase))
            ?.Content
            .Trim()
            ?? string.Empty;

        var relationships = AdrReference
            .FindAll(document)
            .Select(reference => Path.GetFileName(reference.ResolvedPath))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var builder = new StringBuilder();
        builder.Append(role)
            .Append(" ADR ")
            .Append(id)
            .Append(" (")
            .Append(document.FileName)
            .Append(')')
            .Append(Environment.NewLine)
            .Append("Status: ")
            .Append(document.Status ?? string.Empty)
            .Append(Environment.NewLine)
            .Append("Decision:")
            .Append(Environment.NewLine)
            .Append(decision)
            .Append(Environment.NewLine)
            .Append("Local ADR links:")
            .Append(Environment.NewLine);

        if (relationships.Length == 0)
        {
            builder.Append("none");
        }
        else
        {
            foreach (var relationship in relationships)
            {
                builder.Append("- ")
                    .Append(relationship)
                    .Append(Environment.NewLine);
            }

            builder.Length -= Environment.NewLine.Length;
        }

        return builder.ToString();
    }
}
