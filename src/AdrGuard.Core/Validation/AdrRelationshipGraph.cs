using AdrGuard.Model;
using System.Globalization;

namespace AdrGuard.Validation;

internal enum AdrRelationshipResolution
{
    Resolved,
    Unresolved,
    Ambiguous,
}

internal enum AdrRelationshipOrigin
{
    Section,
    StatusText,
    MadrMetadata,
}

internal sealed record AdrRelationshipEdge(
    AdrDocument Source,
    string Type,
    string DeclaredTarget,
    IReadOnlyList<AdrDocument> Targets,
    AdrRelationshipResolution Resolution,
    AdrRelationshipOrigin Origin,
    int? Line = null,
    int? Column = null);

internal sealed record AdrRelationshipGraph(
    IReadOnlyList<AdrDocument> Nodes,
    IReadOnlyList<AdrRelationshipEdge> Relationships);

internal static class AdrRelationshipGraphBuilder
{
    internal static AdrRelationshipGraph Build(
        IReadOnlyList<AdrDocument> documents,
        AdrValidationOptions options)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(options);

        var nodes = documents
            .OrderBy(document => document.FilePath, StringComparer.Ordinal)
            .ToArray();
        var byPath = nodes.ToLookup(
            document => Path.GetFullPath(document.FilePath),
            PathComparer);
        var relationships = new List<AdrRelationshipEdge>();

        foreach (var source in nodes)
        {
            AddSectionRelationships(source, "superseded-by", ["Superseded by"], byPath, options, relationships);
            AddSectionRelationships(source, "supersedes", ["Supersedes"], byPath, options, relationships);
            AddSectionRelationships(source, "depends-on", ["Depends on", "Dependencies"], byPath, options, relationships);

            if (options.ConventionalSupersession)
            {
                AddReferences(
                    source,
                    "superseded-by",
                    AdrReference.FindInStatusText(source, ReferenceRoot(source, options.RepositoryRoot)),
                    AdrRelationshipOrigin.StatusText,
                    byPath,
                    relationships);
            }

            if (TryParseMadrSupersedingId(source.Status, out var targetId))
            {
                var targets = nodes.Where(document => document.Id == targetId).ToArray();
                relationships.Add(CreateEdge(
                    source,
                    "superseded-by",
                    $"ADR-{targetId:D4}",
                    targets,
                    AdrRelationshipOrigin.MadrMetadata));
            }
        }

        return new AdrRelationshipGraph(
            nodes,
            relationships
                .OrderBy(edge => edge.Source.FilePath, StringComparer.Ordinal)
                .ThenBy(edge => edge.Type, StringComparer.Ordinal)
                .ThenBy(edge => edge.DeclaredTarget, StringComparer.Ordinal)
                .ThenBy(edge => edge.Line)
                .ThenBy(edge => edge.Column)
                .ToArray());
    }

    internal static bool TryParseMadrSupersedingId(string? status, out int id)
    {
        const string prefix = "superseded by ADR-";
        id = default;
        return status?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true
            && int.TryParse(
                status[prefix.Length..].Trim(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out id)
            && id > 0;
    }

    internal static bool IsRelationshipDiagnostic(string code) => code is
        ValidationCodes.BrokenReference
        or ValidationCodes.MissingSupersededBy
        or ValidationCodes.DuplicateId
        or ValidationCodes.SupersessionCycle
        or ValidationCodes.SelfSupersession
        or ValidationCodes.MultipleSuperseders
        or ValidationCodes.InconsistentSupersession
        or ValidationCodes.InactiveDependency;

    private static void AddSectionRelationships(
        AdrDocument source,
        string type,
        IReadOnlyList<string> headings,
        ILookup<string, AdrDocument> byPath,
        AdrValidationOptions options,
        List<AdrRelationshipEdge> output) =>
        AddReferences(
            source,
            type,
            AdrReference.FindInSections(source, headings, ReferenceRoot(source, options.RepositoryRoot)),
            AdrRelationshipOrigin.Section,
            byPath,
            output);

    private static void AddReferences(
        AdrDocument source,
        string type,
        IReadOnlyList<AdrReference> references,
        AdrRelationshipOrigin origin,
        ILookup<string, AdrDocument> byPath,
        List<AdrRelationshipEdge> output)
    {
        foreach (var reference in references)
        {
            var targets = reference.ResolvedPath.Length == 0
                ? []
                : byPath[Path.GetFullPath(reference.ResolvedPath)].ToArray();
            output.Add(CreateEdge(
                source,
                type,
                reference.Target,
                targets,
                origin,
                reference.Line,
                reference.Column));
        }
    }

    private static AdrRelationshipEdge CreateEdge(
        AdrDocument source,
        string type,
        string declaredTarget,
        AdrDocument[] targets,
        AdrRelationshipOrigin origin,
        int? line = null,
        int? column = null) =>
        new(
            source,
            type,
            declaredTarget,
            targets,
            targets.Length switch
            {
                1 => AdrRelationshipResolution.Resolved,
                > 1 => AdrRelationshipResolution.Ambiguous,
                _ => AdrRelationshipResolution.Unresolved,
            },
            origin,
            line,
            column);

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static string? ReferenceRoot(AdrDocument source, string? repositoryRoot)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot)) return null;
        var root = Path.GetFullPath(repositoryRoot);
        var path = Path.GetFullPath(source.FilePath);
        var relative = Path.GetRelativePath(root, path);
        return relative != ".."
            && !Path.IsPathRooted(relative)
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal)
                ? root
                : null;
    }
}
