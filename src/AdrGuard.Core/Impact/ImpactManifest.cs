using AdrGuard.Model;

namespace AdrGuard.Impact;

internal enum ImpactRelationship
{
    Governs,
    Implements,
    Constrains,
    DependsOn,
}

internal enum ImpactDecisionResolutionKind
{
    Active,
    Proposed,
    Inactive,
    Unknown,
}

internal sealed record ImpactDecisionReference(
    string StableId,
    string Path);

internal sealed record ImpactDecisionResolution(
    ImpactDecisionResolutionKind Kind,
    string? Status,
    string? Detail,
    AdrDocument? Document);

internal sealed record ImpactMapping(
    ImpactDecisionReference Decision,
    IReadOnlyList<string> Patterns,
    ImpactRelationship Relationship,
    string Reason,
    ImpactDecisionResolution Resolution);

internal sealed record ImpactMappingManifest(
    string SchemaVersion,
    string RepositoryRoot,
    string ManifestPath,
    IReadOnlyList<ImpactMapping> Mappings)
{
    internal const string CurrentSchemaVersion = "1.0";
}

internal sealed class ImpactManifestException : Exception
{
    internal ImpactManifestException(string message)
        : base(message)
    {
    }

    internal ImpactManifestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
