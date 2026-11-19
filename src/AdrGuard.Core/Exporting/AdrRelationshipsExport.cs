using System.Text.Json.Serialization;

namespace AdrGuard.Exporting;

internal static class AdrRelationshipsExportContract
{
    internal const string SchemaVersion = "1.0";
}

internal sealed record AdrRelationshipsExport(
    string SchemaVersion,
    IReadOnlyList<AdrRelationshipExportAdr> Adrs,
    IReadOnlyList<AdrRelationshipExportDeclaration> Relationships,
    IReadOnlyList<AdrRelationshipExportIssue> Issues);

internal sealed record AdrRelationshipExportAdr(
    string Identifier,
    string Path,
    string? Title,
    AdrRelationshipExportStatus? Status);

[JsonConverter(typeof(JsonStringEnumConverter<AdrRelationshipExportStatus>))]
internal enum AdrRelationshipExportStatus
{
    Accepted,
    Proposed,
    Deprecated,
    Superseded,
}

internal sealed record AdrRelationshipExportDeclaration(
    string SourceIdentifier,
    string SourcePath,
    AdrRelationshipExportType Type,
    string DeclaredTarget,
    string? TargetIdentifier,
    string? TargetPath,
    AdrRelationshipExportResolution Resolution);

[JsonConverter(typeof(JsonStringEnumConverter<AdrRelationshipExportType>))]
internal enum AdrRelationshipExportType
{
    [JsonStringEnumMemberName("supersedes")]
    Supersedes,

    [JsonStringEnumMemberName("superseded-by")]
    SupersededBy,

    [JsonStringEnumMemberName("depends-on")]
    DependsOn,
}

[JsonConverter(typeof(JsonStringEnumConverter<AdrRelationshipExportResolution>))]
internal enum AdrRelationshipExportResolution
{
    [JsonStringEnumMemberName("resolved")]
    Resolved,

    [JsonStringEnumMemberName("ambiguous")]
    Ambiguous,

    [JsonStringEnumMemberName("unresolved")]
    Unresolved,
}

internal sealed record AdrRelationshipExportIssue(
    string Code,
    string Message,
    string Path,
    AdrRelationshipExportIssueSeverity Severity);

[JsonConverter(typeof(JsonStringEnumConverter<AdrRelationshipExportIssueSeverity>))]
internal enum AdrRelationshipExportIssueSeverity
{
    [JsonStringEnumMemberName("warning")]
    Warning,

    [JsonStringEnumMemberName("error")]
    Error,
}
