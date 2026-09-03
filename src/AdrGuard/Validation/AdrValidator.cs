using AdrGuard.Model;

namespace AdrGuard.Validation;

internal static class AdrValidator
{
    private static readonly string[] AllowedStatuses =
    [
        "Proposed",
        "Accepted",
        "Deprecated",
        "Superseded",
    ];

    private static readonly string[] RequiredSections =
    [
        "Context",
        "Decision",
        "Consequences",
    ];

    internal static ValidationResult Validate(IReadOnlyList<AdrDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var issues = new List<ValidationIssue>();
        var knownPaths = documents
            .Select(document => Path.GetFullPath(document.FilePath))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var document in documents)
        {
            ValidateDocument(document, knownPaths, issues);
        }

        ValidateDuplicateIds(documents, issues);

        return new ValidationResult(
            issues
                .OrderBy(issue => issue.FilePath, StringComparer.Ordinal)
                .ThenBy(issue => issue.Code, StringComparer.Ordinal)
                .ThenBy(issue => issue.Message, StringComparer.Ordinal)
                .ToArray());
    }

    private static void ValidateDocument(
        AdrDocument document,
        HashSet<string> knownPaths,
        List<ValidationIssue> issues)
    {
        ValidateFileName(document, issues);
        ValidateTitle(document, issues);
        ValidateStatus(document, issues);
        ValidateRequiredSections(document, issues);
        ValidateReferences(document, knownPaths, issues);
        ValidateSupersededBy(document, knownPaths, issues);
    }

    private static void ValidateFileName(
        AdrDocument document,
        List<ValidationIssue> issues)
    {
        if (IsValidFileName(document.FileName))
        {
            return;
        }

        issues.Add(new ValidationIssue(
            ValidationCodes.InvalidFileName,
            document.FilePath,
            $"File name '{document.FileName}' must match 'NNNN-lowercase-kebab-case.md' with an ID greater than zero."));
    }

    private static bool IsValidFileName(string fileName)
    {
        if (!string.Equals(Path.GetExtension(fileName), ".md", StringComparison.Ordinal))
        {
            return false;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        if (stem.Length < 6 || stem[4] != '-')
        {
            return false;
        }

        var idText = stem[..4];
        if (idText.Any(character => !char.IsAsciiDigit(character))
            || !int.TryParse(idText, out var id)
            || id <= 0)
        {
            return false;
        }

        var slug = stem[5..];
        if (slug.Length == 0
            || slug[0] == '-'
            || slug[^1] == '-')
        {
            return false;
        }

        return slug.All(character =>
            character == '-'
            || char.IsAsciiDigit(character)
            || character is >= 'a' and <= 'z')
            && !slug.Contains("--", StringComparison.Ordinal);
    }

    private static void ValidateTitle(
        AdrDocument document,
        List<ValidationIssue> issues)
    {
        if (!string.IsNullOrWhiteSpace(document.Title))
        {
            return;
        }

        issues.Add(new ValidationIssue(
            ValidationCodes.MissingTitle,
            document.FilePath,
            "ADR must define a level-one title."));
    }

    private static void ValidateStatus(
        AdrDocument document,
        List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(document.Status))
        {
            issues.Add(new ValidationIssue(
                ValidationCodes.MissingStatus,
                document.FilePath,
                "ADR must define a non-empty 'Status' section."));
            return;
        }

        if (AllowedStatuses.Contains(document.Status, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        issues.Add(new ValidationIssue(
            ValidationCodes.InvalidStatus,
            document.FilePath,
            $"Status '{document.Status}' is invalid. Allowed values: {string.Join(", ", AllowedStatuses)}."));
    }

    private static void ValidateRequiredSections(
        AdrDocument document,
        List<ValidationIssue> issues)
    {
        foreach (var requiredSection in RequiredSections)
        {
            var section = document.Sections.FirstOrDefault(candidate =>
                string.Equals(candidate.Heading, requiredSection, StringComparison.OrdinalIgnoreCase));

            if (section is not null && !string.IsNullOrWhiteSpace(section.Content))
            {
                continue;
            }

            issues.Add(new ValidationIssue(
                ValidationCodes.MissingSection,
                document.FilePath,
                $"ADR must define a non-empty '{requiredSection}' section."));
        }
    }

    private static void ValidateDuplicateIds(
        IReadOnlyList<AdrDocument> documents,
        List<ValidationIssue> issues)
    {
        var duplicateGroups = documents
            .Where(document => document.Id.HasValue)
            .GroupBy(document => document.Id!.Value)
            .Where(group => group.Count() > 1);

        foreach (var group in duplicateGroups)
        {
            var paths = group
                .Select(document => document.FilePath)
                .Order(StringComparer.Ordinal)
                .ToArray();

            foreach (var document in group)
            {
                issues.Add(new ValidationIssue(
                    ValidationCodes.DuplicateId,
                    document.FilePath,
                    $"ADR ID {group.Key:D4} is duplicated by: {string.Join(", ", paths)}."));
            }
        }
    }

    private static void ValidateReferences(
        AdrDocument document,
        HashSet<string> knownPaths,
        List<ValidationIssue> issues)
    {
        foreach (var reference in AdrReference.FindAll(document))
        {
            if (knownPaths.Contains(reference.ResolvedPath))
            {
                continue;
            }

            issues.Add(new ValidationIssue(
                ValidationCodes.BrokenReference,
                document.FilePath,
                $"Reference '{reference.Target}' does not resolve to an ADR in the validated set."));
        }
    }

    private static void ValidateSupersededBy(
        AdrDocument document,
        HashSet<string> knownPaths,
        List<ValidationIssue> issues)
    {
        if (!string.Equals(document.Status, "Superseded", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var section = document.Sections.FirstOrDefault(candidate =>
            string.Equals(candidate.Heading, "Superseded by", StringComparison.OrdinalIgnoreCase));

        if (section is null || string.IsNullOrWhiteSpace(section.Content))
        {
            AddMissingSupersededByIssue(document, issues);
            return;
        }

        var references = AdrReference.FindAll(
            document with { Sections = [section] });

        if (references.Any(reference => knownPaths.Contains(reference.ResolvedPath)))
        {
            return;
        }

        AddMissingSupersededByIssue(document, issues);
    }

    private static void AddMissingSupersededByIssue(
        AdrDocument document,
        List<ValidationIssue> issues)
    {
        issues.Add(new ValidationIssue(
            ValidationCodes.MissingSupersededBy,
            document.FilePath,
            "An ADR with status 'Superseded' must define a 'Superseded by' section linking to an existing ADR."));
    }
}
