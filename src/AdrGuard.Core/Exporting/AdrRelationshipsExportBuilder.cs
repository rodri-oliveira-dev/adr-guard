using System.Globalization;
using AdrGuard.Configuration;
using AdrGuard.Model;
using AdrGuard.Validation;

namespace AdrGuard.Exporting;

internal static class AdrRelationshipsExportBuilder
{
    internal static AdrRelationshipsExport Create(
        IReadOnlyList<AdrDocument> documents,
        string repositoryRoot,
        AdrValidationOptions options,
        bool makeIssueMessagesPortable = true)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentNullException.ThrowIfNull(options);

        var root = Path.GetFullPath(repositoryRoot);
        var effectiveOptions = options with { RepositoryRoot = root };
        var normalizedDocuments = AdrStatusResolver.ForFormat(
            documents.Select(effectiveOptions.EffectiveFilenamePolicy.NormalizeIdentity).ToArray(),
            effectiveOptions.Format);
        var byPath = normalizedDocuments.ToLookup(
            document => Path.GetFullPath(document.FilePath),
            PathComparer);
        var documentsWithAdrs = normalizedDocuments
            .Select(document => (
                Document: document,
                Adr: new AdrRelationshipExportAdr(
                    Identifier(document),
                    Relative(root, document.FilePath),
                    document.Title,
                    Status(document, effectiveOptions))))
            .ToArray();
        var adrs = documentsWithAdrs
            .Select(item => item.Adr)
            .OrderBy(adr => adr.Identifier, StringComparer.Ordinal)
            .ThenBy(adr => adr.Path, StringComparer.Ordinal)
            .ToArray();
        var relationships = new List<AdrRelationshipExportDeclaration>();

        foreach (var source in normalizedDocuments)
        {
            AddSectionRelationships(
                source,
                AdrRelationshipExportType.SupersededBy,
                ["Superseded by"],
                root,
                byPath,
                relationships);
            AddSectionRelationships(
                source,
                AdrRelationshipExportType.Supersedes,
                ["Supersedes"],
                root,
                byPath,
                relationships);
            AddSectionRelationships(
                source,
                AdrRelationshipExportType.DependsOn,
                ["Depends on", "Dependencies"],
                root,
                byPath,
                relationships);

            if (effectiveOptions.ConventionalSupersession)
            {
                AddReferences(
                    source,
                    AdrRelationshipExportType.SupersededBy,
                    AdrReference.FindInStatusText(source, root),
                    root,
                    byPath,
                    relationships);
            }

            AddMadrSupersession(source, normalizedDocuments, root, effectiveOptions.Format, relationships);
        }

        var issues = AdrValidator.Validate(normalizedDocuments, additionalKnownPaths: null, effectiveOptions)
            .Issues
            .Select(issue => new AdrRelationshipExportIssue(
                issue.Code,
                makeIssueMessagesPortable
                    ? PortableIssueMessage(root, issue.Message, normalizedDocuments)
                    : issue.Message,
                Relative(root, issue.FilePath),
                issue.Severity == ValidationSeverity.Warning
                    ? AdrRelationshipExportIssueSeverity.Warning
                    : AdrRelationshipExportIssueSeverity.Error))
            .ToList();

        if (effectiveOptions.Format == AdrFormat.Madr4)
        {
            foreach (var item in documentsWithAdrs.Where(item => item.Adr.Status is null))
            {
                var path = item.Adr.Path;
                if (issues.Any(issue => issue.Path == path
                    && issue.Code is ValidationCodes.MissingStatus or ValidationCodes.InvalidStatus))
                {
                    continue;
                }

                var missing = string.IsNullOrWhiteSpace(item.Document.Status);
                issues.Add(new AdrRelationshipExportIssue(
                    missing ? ValidationCodes.MissingStatus : ValidationCodes.InvalidStatus,
                    missing
                        ? "MADR 4.0 status is missing; export status is unknown."
                        : $"MADR 4.0 status '{item.Document.Status}' is not recognized by relationships export schema 1.0.",
                    path,
                    AdrRelationshipExportIssueSeverity.Error));
            }
        }

        return new AdrRelationshipsExport(
            AdrRelationshipsExportContract.SchemaVersion,
            adrs,
            relationships
                .Distinct()
                .OrderBy(relationship => relationship.SourceIdentifier, StringComparer.Ordinal)
                .ThenBy(relationship => relationship.SourcePath, StringComparer.Ordinal)
                .ThenBy(relationship => TypeName(relationship.Type), StringComparer.Ordinal)
                .ThenBy(relationship => relationship.DeclaredTarget, StringComparer.Ordinal)
                .ToArray(),
            issues
                .OrderBy(issue => issue.Path, StringComparer.Ordinal)
                .ThenBy(issue => issue.Code, StringComparer.Ordinal)
                .ThenBy(issue => issue.Message, StringComparer.Ordinal)
                .ToArray());
    }

    internal static string Identifier(AdrDocument document) =>
        document.StableId
        ?? (document.Id is null
            ? $"path:{document.FileName}"
            : $"ADR-{document.Id.Value.ToString(CultureInfo.InvariantCulture)}");

    private static void AddSectionRelationships(
        AdrDocument source,
        AdrRelationshipExportType type,
        IReadOnlyList<string> headings,
        string repositoryRoot,
        ILookup<string, AdrDocument> byPath,
        List<AdrRelationshipExportDeclaration> output) =>
        AddReferences(
            source,
            type,
            AdrReference.FindInSections(source, headings, repositoryRoot),
            repositoryRoot,
            byPath,
            output);

    private static void AddReferences(
        AdrDocument source,
        AdrRelationshipExportType type,
        IReadOnlyList<AdrReference> references,
        string repositoryRoot,
        ILookup<string, AdrDocument> byPath,
        List<AdrRelationshipExportDeclaration> output)
    {
        foreach (var reference in references)
        {
            var targets = reference.ResolvedPath.Length == 0
                ? []
                : byPath[Path.GetFullPath(reference.ResolvedPath)].ToArray();
            output.Add(CreateRelationship(
                source,
                type,
                reference.Target,
                targets,
                repositoryRoot));
        }
    }

    private static void AddMadrSupersession(
        AdrDocument source,
        IReadOnlyList<AdrDocument> documents,
        string repositoryRoot,
        AdrFormat format,
        List<AdrRelationshipExportDeclaration> output)
    {
        if (format != AdrFormat.Madr4
            || !TryParseMadrSupersedingId(source.Status, out var id))
        {
            return;
        }

        output.Add(CreateRelationship(
            source,
            AdrRelationshipExportType.SupersededBy,
            $"ADR-{id}",
            documents.Where(document => document.Id == id).ToArray(),
            repositoryRoot));
    }

    private static AdrRelationshipExportDeclaration CreateRelationship(
        AdrDocument source,
        AdrRelationshipExportType type,
        string declaredTarget,
        AdrDocument[] targets,
        string repositoryRoot) =>
        new(
            Identifier(source),
            Relative(repositoryRoot, source.FilePath),
            type,
            declaredTarget,
            targets.Length == 1 ? Identifier(targets[0]) : null,
            targets.Length == 1 ? Relative(repositoryRoot, targets[0].FilePath) : null,
            targets.Length switch
            {
                1 => AdrRelationshipExportResolution.Resolved,
                > 1 => AdrRelationshipExportResolution.Ambiguous,
                _ => AdrRelationshipExportResolution.Unresolved,
            });

    private static AdrRelationshipExportStatus? Status(
        AdrDocument document,
        AdrValidationOptions options)
    {
        if (options.EffectiveLifecycle.TryResolve(document.Status, out var kind))
        {
            return kind switch
            {
                AdrLifecycleKind.Accepted => AdrRelationshipExportStatus.Accepted,
                AdrLifecycleKind.Proposed => AdrRelationshipExportStatus.Proposed,
                AdrLifecycleKind.Deprecated => AdrRelationshipExportStatus.Deprecated,
                AdrLifecycleKind.Superseded => AdrRelationshipExportStatus.Superseded,
                _ => null,
            };
        }

        if ((options.ConventionalSupersession
                && AdrReference.FindInStatusText(document, options.RepositoryRoot).Count == 1)
            || (options.Format == AdrFormat.Madr4
                && TryParseMadrSupersedingId(document.Status, out _)))
        {
            return AdrRelationshipExportStatus.Superseded;
        }

        return null;
    }

    private static bool TryParseMadrSupersedingId(string? status, out int id)
    {
        const string prefix = "superseded by ADR-";
        id = 0;
        return status?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true
            && int.TryParse(
                status[prefix.Length..].Trim(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out id)
            && id > 0;
    }

    private static string PortableIssueMessage(
        string repositoryRoot,
        string message,
        IReadOnlyList<AdrDocument> documents)
    {
        foreach (var documentPath in documents
                     .Select(document => Path.GetFullPath(document.FilePath))
                     .Distinct(PathComparer)
                     .OrderByDescending(path => path.Length))
        {
            message = message.Replace(
                documentPath,
                Relative(repositoryRoot, documentPath),
                PathComparison);
        }

        return message;
    }

    private static string Relative(string repositoryRoot, string path)
    {
        var fullPath = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(repositoryRoot, fullPath);
        RepositoryPath.ResolveContained(repositoryRoot, relative);
        var portable = relative.Replace(Path.DirectorySeparatorChar, '/');
        if (portable.Contains('\\'))
        {
            throw new InvalidDataException(
                $"Repository path '{portable}' cannot be represented by the portable export contract.");
        }

        return portable;
    }

    private static string TypeName(AdrRelationshipExportType type) => type switch
    {
        AdrRelationshipExportType.DependsOn => "depends-on",
        AdrRelationshipExportType.SupersededBy => "superseded-by",
        AdrRelationshipExportType.Supersedes => "supersedes",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported ADR relationship type."),
    };

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
