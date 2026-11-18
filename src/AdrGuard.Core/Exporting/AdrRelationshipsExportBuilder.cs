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
        AdrValidationOptions options)
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
        var adrs = normalizedDocuments
            .Select(document => new AdrRelationshipExportAdr(
                Identifier(document),
                Relative(root, document.FilePath),
                document.Title,
                Status(document, effectiveOptions)))
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
                issue.Message,
                Relative(root, issue.FilePath),
                issue.Severity == ValidationSeverity.Warning
                    ? AdrRelationshipExportIssueSeverity.Warning
                    : AdrRelationshipExportIssueSeverity.Error))
            .ToArray();

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
            issues);
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
        const string prefix = "superseded by ADR-";
        if (format != AdrFormat.Madr4
            || source.Status?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) != true
            || !int.TryParse(
                source.Status[prefix.Length..].Trim(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var id)
            || id <= 0)
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
                && document.Status?.StartsWith("superseded by ADR-", StringComparison.OrdinalIgnoreCase) == true))
        {
            return AdrRelationshipExportStatus.Superseded;
        }

        return null;
    }

    private static string Relative(string repositoryRoot, string path)
    {
        var fullPath = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(repositoryRoot, fullPath);
        RepositoryPath.ResolveContained(repositoryRoot, relative);
        return relative.Replace(Path.DirectorySeparatorChar, '/');
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
}
