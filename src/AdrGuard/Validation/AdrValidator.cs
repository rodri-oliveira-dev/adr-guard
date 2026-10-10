using AdrGuard.Model;

namespace AdrGuard.Validation;

internal static class AdrValidator
{
    private static readonly string[] RequiredSections =
    [
        "Context",
        "Decision",
        "Consequences",
    ];

    private static readonly string[] Madr4RequiredSections =
    [
        "Context and Problem Statement",
        "Considered Options",
        "Decision Outcome",
    ];

    private static readonly string[] CanonicalSections =
    [
        "Status",
        "Context",
        "Decision",
        "Consequences",
    ];

    internal static ValidationResult Validate(
        IReadOnlyList<AdrDocument> documents) =>
        Validate(
            documents,
            additionalKnownPaths: null,
            AdrFormat.Canonical);

    internal static ValidationResult Validate(
        IReadOnlyList<AdrDocument> documents,
        AdrFormat format) =>
        Validate(documents, additionalKnownPaths: null, format);

    internal static ValidationResult Validate(
        IReadOnlyList<AdrDocument> documents,
        IEnumerable<string>? additionalKnownPaths) =>
        Validate(documents, additionalKnownPaths, AdrFormat.Canonical);

    internal static ValidationResult Validate(
        IReadOnlyList<AdrDocument> documents,
        IEnumerable<string>? additionalKnownPaths,
        AdrFormat format,
        string? repositoryRoot = null) =>
        Validate(
            documents,
            additionalKnownPaths,
            new AdrValidationOptions(format, repositoryRoot));

    internal static ValidationResult Validate(
        IReadOnlyList<AdrDocument> documents,
        IEnumerable<string>? additionalKnownPaths,
        AdrValidationOptions options)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(options);

        // Interpret identities using the selected filename convention before
        // duplicate checks, reference relationships and filename validation.
        documents = documents
            .Select(options.EffectiveFilenamePolicy.NormalizeIdentity)
            .ToArray();

        // Keep metadata status specific to the explicitly selected MADR format.
        documents = AdrStatusResolver.ForFormat(documents, options.Format);
        var issues = new List<ValidationIssue>();
        var knownPaths = documents
            .Select(document => Path.GetFullPath(document.FilePath))
            .ToHashSet(StringComparer.Ordinal);

        if (additionalKnownPaths is not null)
        {
            foreach (var path in additionalKnownPaths)
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    knownPaths.Add(Path.GetFullPath(path));
                }
            }
        }

        foreach (var document in documents)
        {
            ValidateDocument(document, knownPaths, issues, options);
        }

        ValidateDuplicateIds(documents, issues);
        ValidateDuplicateStableIds(documents, issues);
        AdrRelationshipValidator.Validate(documents, issues, options);

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
        List<ValidationIssue> issues,
        AdrValidationOptions options)
    {
        ValidateFileName(document, issues, options.EffectiveFilenamePolicy);
        ValidateTitle(document, issues);
        if (options.Format == AdrFormat.Canonical)
        {
            ValidateUniqueCanonicalSections(document, issues);
            ValidateStatus(document, issues, options);
            ValidateRequiredSections(document, RequiredSections, issues);
        }
        else
        {
            ValidateMadr4(document, issues);
        }
        ValidateReferences(document, knownPaths, issues, options.RepositoryRoot);
        ValidateSupersededBy(document, knownPaths, issues, options);
        ValidatePlaceholders(document, issues, options.PlaceholderPolicy);
        ValidateMetadata(document, issues, options.ValidateMetadata);
    }

    private static void ValidateMadr4(
        AdrDocument document,
        List<ValidationIssue> issues)
    {
        ValidateUniqueSections(document, Madr4RequiredSections, issues);
        ValidateRequiredSections(document, Madr4RequiredSections, issues);

        if (document.Metadata?.ContainsKey("status") == true
            && string.IsNullOrWhiteSpace(document.Status))
        {
            issues.Add(new ValidationIssue(
                ValidationCodes.MissingStatus,
                document.FilePath,
                "MADR 4.0 status metadata, when present, must be non-empty."));
        }
    }

    private static void ValidateUniqueSections(
        AdrDocument document,
        IEnumerable<string> sectionNames,
        List<ValidationIssue> issues)
    {
        foreach (var sectionName in sectionNames)
        {
            var count = document.Sections.Count(section =>
                section.Level == 2
                && string.Equals(section.Heading, sectionName, StringComparison.OrdinalIgnoreCase));
            if (count > 1)
            {
                issues.Add(new ValidationIssue(
                    ValidationCodes.DuplicateCanonicalSection,
                    document.FilePath,
                    $"ADR must define exactly one level-two '{sectionName}' section; found {count}."));
            }
        }
    }

    private static void ValidateFileName(
        AdrDocument document,
        List<ValidationIssue> issues,
        AdrFilenamePolicy filenamePolicy)
    {
        if (filenamePolicy.IsValid(document))
        {
            return;
        }

        issues.Add(new ValidationIssue(
            ValidationCodes.InvalidFileName,
            document.FilePath,
            $"File name '{document.FileName}' does not match the configured '{filenamePolicy.Convention}' filename policy."));
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

    private static void ValidateUniqueCanonicalSections(
        AdrDocument document,
        List<ValidationIssue> issues)
    {
        foreach (var canonicalSection in CanonicalSections)
        {
            var count = document.Sections.Count(section =>
                section.Level == 2
                && string.Equals(
                    section.Heading,
                    canonicalSection,
                    StringComparison.OrdinalIgnoreCase));

            if (count <= 1)
            {
                continue;
            }

            issues.Add(new ValidationIssue(
                ValidationCodes.DuplicateCanonicalSection,
                document.FilePath,
                $"ADR must define exactly one level-two '{canonicalSection}' section; found {count}."));
        }
    }

    private static void ValidateStatus(
        AdrDocument document,
        List<ValidationIssue> issues,
        AdrValidationOptions options)
    {
        var statusSection = document.Sections
            .FirstOrDefault(section =>
                section.Level == 2
                && string.Equals(
                    section.Heading,
                    "Status",
                    StringComparison.OrdinalIgnoreCase));

        var status = statusSection?.Content
            .Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(status))
        {
            issues.Add(new ValidationIssue(
                ValidationCodes.MissingStatus,
                document.FilePath,
                "ADR must define a non-empty level-two 'Status' section."));
            return;
        }

        if (options.EffectiveLifecycle.TryResolve(status, out _)
            || options.ConventionalSupersession
                && AdrReference.FindInStatusText(document, options.RepositoryRoot).Count == 1)
        {
            return;
        }

        issues.Add(new ValidationIssue(
            ValidationCodes.InvalidStatus,
            document.FilePath,
            $"Status '{status}' is invalid. Allowed values: {string.Join(", ", options.EffectiveLifecycle.StatusNames)}."));
    }

    private static void ValidateRequiredSections(
        AdrDocument document,
        IEnumerable<string> requiredSections,
        List<ValidationIssue> issues)
    {
        foreach (var requiredSection in requiredSections)
        {
            var section = document.Sections.FirstOrDefault(candidate =>
                candidate.Level == 2
                && string.Equals(
                    candidate.Heading,
                    requiredSection,
                    StringComparison.OrdinalIgnoreCase));

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

    private static void ValidateDuplicateStableIds(
        IReadOnlyList<AdrDocument> documents,
        List<ValidationIssue> issues)
    {
        foreach (var group in documents
                     .Where(document => document.Id is null)
                     .GroupBy(
                         document => document.StableId ?? $"slug:{Path.GetFileNameWithoutExtension(document.FileName).ToLowerInvariant()}",
                         StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            foreach (var document in group)
            {
                issues.Add(new ValidationIssue(
                    ValidationCodes.DuplicateId,
                    document.FilePath,
                    $"Stable ADR identity '{group.Key}' is ambiguous."));
            }
        }
    }

    private static void ValidatePlaceholders(
        AdrDocument document,
        List<ValidationIssue> issues,
        PlaceholderPolicy policy)
    {
        if (policy == PlaceholderPolicy.Off) return;
        var bracketedTokens = new[] { "[EDIT]", "[EDITAR]" };
        const string wordMarkers = @"(?<![\p{L}\p{N}_])(?:TODO|TBD)(?![\p{L}\p{N}_])";
        var markerOptions = System.Text.RegularExpressions.RegexOptions.IgnoreCase
            | System.Text.RegularExpressions.RegexOptions.CultureInvariant;
        foreach (var section in document.Sections)
        {
            var content = RemoveFencedContent(section.Content);
            var token = bracketedTokens.FirstOrDefault(candidate =>
                content.Contains(candidate, StringComparison.OrdinalIgnoreCase));
            if (token is null)
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    content, wordMarkers, markerOptions, TimeSpan.FromMilliseconds(100));
                if (match.Success) token = match.Value.ToUpperInvariant();
            }

            if (token is null && !System.Text.RegularExpressions.Regex.IsMatch(
                content, @"\{\{[^{}\r\n]+\}\}",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100)))
                continue;
            issues.Add(new ValidationIssue(
                ValidationCodes.UnresolvedPlaceholder,
                document.FilePath,
                $"Section '{section.Heading}' contains unresolved authoring placeholder '{token ?? "{{...}}"}'.",
                policy == PlaceholderPolicy.Warn ? ValidationSeverity.Warning : ValidationSeverity.Error));
        }
    }

    private static string RemoveFencedContent(string content)
    {
        var result = new System.Text.StringBuilder();
        var fenced = false;
        char marker = default;
        foreach (var line in content.Split(['\r', '\n']))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("```", StringComparison.Ordinal)
                || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                if (!fenced) { fenced = true; marker = trimmed[0]; }
                else if (trimmed[0] == marker) fenced = false;
                continue;
            }
            if (!fenced) result.AppendLine(line);
        }
        return result.ToString();
    }

    private static void ValidateMetadata(AdrDocument document, List<ValidationIssue> issues, bool enabled)
    {
        if (!enabled) return;
        foreach (var error in document.MetadataErrors ?? [])
            issues.Add(new ValidationIssue(ValidationCodes.InvalidMetadata, document.FilePath, error));

        var metadata = document.DecisionMetadata;
        if (metadata is null) return;
        foreach (var (name, value) in new[] { ("date", metadata.DecisionDate), ("last-reviewed", metadata.LastReviewedDate) })
        {
            if (value is not null && !DateOnly.TryParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                issues.Add(new ValidationIssue(ValidationCodes.InvalidMetadata, document.FilePath, $"Metadata '{name}' must use YYYY-MM-DD."));
        }
        foreach (var link in metadata.FollowUps)
        {
            if (!Uri.TryCreate(link, UriKind.RelativeOrAbsolute, out _))
                issues.Add(new ValidationIssue(ValidationCodes.InvalidMetadata, document.FilePath, $"Follow-up link '{link}' is malformed."));
        }
    }

    private static void ValidateReferences(
        AdrDocument document,
        HashSet<string> knownPaths,
        List<ValidationIssue> issues,
        string? repositoryRoot)
    {
        foreach (var reference in AdrReference.FindAll(document, repositoryRoot))
        {
            if (reference.ResolvedPath.Length > 0
                && (reference.Kind == AdrReferenceKind.Relationship
                    ? knownPaths.Contains(reference.ResolvedPath)
                    : knownPaths.Contains(reference.ResolvedPath) || File.Exists(reference.ResolvedPath)))
            {
                continue;
            }

            issues.Add(new ValidationIssue(
                ValidationCodes.BrokenReference,
                document.FilePath,
                reference.InvalidReason is not null
                    ? $"Reference '{reference.Target}' is unsafe: {reference.InvalidReason}"
                    : reference.Kind == AdrReferenceKind.Relationship
                        ? $"ADR relationship '{reference.Target}' does not resolve to an ADR in the validated set."
                        : $"Document link '{reference.Target}' does not resolve to an existing repository file."));
        }
    }

    private static void ValidateSupersededBy(
        AdrDocument document,
        HashSet<string> knownPaths,
        List<ValidationIssue> issues,
        AdrValidationOptions options)
    {
        var conventional = options.ConventionalSupersession
            ? AdrReference.FindInStatusText(document, options.RepositoryRoot)
            : [];
        if ((!options.EffectiveLifecycle.TryResolve(document.Status, out var kind)
                || kind != AdrLifecycleKind.Superseded)
            && conventional.Count == 0)
        {
            return;
        }

        if (conventional.Count == 1
            && knownPaths.Contains(conventional[0].ResolvedPath))
        {
            return;
        }

        var section = document.Sections.FirstOrDefault(candidate =>
            candidate.Level == 2
            && string.Equals(
                candidate.Heading,
                "Superseded by",
                StringComparison.OrdinalIgnoreCase));

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
