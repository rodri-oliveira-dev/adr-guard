namespace AdrGuard.Mcp;

internal sealed record AdrSummary(
    string Identifier,
    string? Title,
    string? Status,
    string Path,
    string Format);

internal sealed record ListAdrsResult(
    string SchemaVersion,
    int Total,
    IReadOnlyList<AdrSummary> Adrs);

internal sealed record GetAdrResult(
    string SchemaVersion,
    AdrSummary Adr,
    bool ContentIsUntrusted,
    string Content);

internal sealed record SearchAdrMatch(
    AdrSummary Adr,
    IReadOnlyList<string> MatchedFields,
    string? Excerpt);

internal sealed record SearchAdrsResult(
    string SchemaVersion,
    string Query,
    bool ExcerptsAreUntrusted,
    int Total,
    IReadOnlyList<SearchAdrMatch> Matches);

internal sealed record AdrRelationshipResult(
    string SourceIdentifier,
    string SourcePath,
    string Type,
    string DeclaredTarget,
    string? TargetIdentifier,
    string? TargetPath,
    string Resolution);

internal sealed record AdrRelationshipIssue(
    string Code,
    string Message,
    string Path,
    string Severity);

internal sealed record GetAdrRelationshipsResult(
    string SchemaVersion,
    IReadOnlyList<AdrRelationshipResult> Relationships,
    IReadOnlyList<AdrRelationshipIssue> Issues);

internal sealed record ValidationPolicyResult(
    string Format,
    string Profile,
    bool ConventionalSupersession,
    string FilenamePolicy,
    string PlaceholderPolicy,
    bool ValidateMetadata);

internal sealed record ValidationDiagnosticResult(
    string Code,
    string Message,
    string Path,
    string Severity);

internal sealed record ValidateAdrsResult(
    string SchemaVersion,
    bool Valid,
    int Files,
    ValidationPolicyResult Policy,
    IReadOnlyList<ValidationDiagnosticResult> Diagnostics);

internal sealed record ImpactToolResult(
    string SchemaVersion,
    bool Advisory,
    ImpactRepositoryResult Repository,
    ImpactSummaryResult Summary,
    ImpactCoverageResult Coverage,
    ImpactUncertaintyResult Uncertainty,
    IReadOnlyList<ImpactChangeResult> ChangedFiles,
    IReadOnlyList<ImpactDecisionResult> Decisions);

internal sealed record ImpactRepositoryResult(string BaseReference, string MergeBase, string ManifestPath);
internal sealed record ImpactSummaryResult(int TotalChanges, int AffectedDecisions, int UnknownDecisions, int NotMatchedDecisions);
internal sealed record ImpactCoverageResult(int AffectedChanges, int UnknownChanges, int NotMatchedChanges);
internal sealed record ImpactUncertaintyResult(bool HasUnknowns, IReadOnlyList<string> DecisionStableIds, string Explanation);
internal sealed record ImpactChangeResult(string Scope, string ChangeType, string? OldPath, string? NewPath, int? Similarity, string Status, IReadOnlyList<string> DecisionStableIds);
internal sealed record ImpactDecisionResult(string StableId, string Path, string? Title, string? LifecycleStatus, string Resolution, string Status, string? Uncertainty, IReadOnlyList<ImpactEvidenceResult> Evidence);
internal sealed record ImpactEvidenceResult(string Scope, string ChangeType, string PathRole, string Path, string Pattern, string Relationship, string Reason);

internal sealed record DriftToolResult(
    string SchemaVersion,
    string Policy,
    DriftRepositoryResult Repository,
    DriftCoverageResult Coverage,
    IReadOnlyList<DriftFindingResult> Findings,
    bool Inconclusive,
    bool PolicyFailed);

internal sealed record DriftRepositoryResult(string RulesManifest, string? Commit);
internal sealed record DriftCoverageResult(int TotalRules, int Pass, int Violations, int Unknown, int NotApplicable);
internal sealed record DriftFindingResult(string Fingerprint, string RuleId, string AdrStableId, string Outcome, string CheckType, string Confidence, string BaselineState, string Explanation, IReadOnlyList<DriftEvidenceResult> Evidence);
internal sealed record DriftEvidenceResult(string Kind, string Source, string? Subject, string? Target, int? Line, string Detail);
