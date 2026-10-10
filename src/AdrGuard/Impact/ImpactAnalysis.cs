using AdrGuard.Git;

namespace AdrGuard.Impact;

internal enum ImpactAssessmentStatus
{
    Affected,
    NotMatched,
    Unknown,
}

internal enum ImpactEvidencePathRole
{
    Current,
    Previous,
}

internal sealed record ImpactEvidence(
    GitChangeScope Scope,
    GitChangeKind ChangeKind,
    ImpactEvidencePathRole PathRole,
    string Path,
    string Pattern,
    ImpactRelationship Relationship,
    string Reason);

internal sealed record ImpactDecisionAssessment(
    ImpactDecisionReference Decision,
    ImpactDecisionResolution Resolution,
    ImpactAssessmentStatus Status,
    IReadOnlyList<ImpactEvidence> Evidence);

internal sealed record ImpactChangeAssessment(
    GitChangedPath Change,
    ImpactAssessmentStatus Status,
    IReadOnlyList<string> DecisionStableIds);

internal sealed record ImpactCoverage(
    int TotalChanges,
    int AffectedChanges,
    int UnknownChanges,
    int NotMatchedChanges);

internal sealed record ImpactAnalysisResult(
    string RepositoryRoot,
    string BaseReference,
    string MergeBase,
    string ManifestPath,
    IReadOnlyList<ImpactDecisionAssessment> Decisions,
    IReadOnlyList<ImpactChangeAssessment> Changes,
    ImpactCoverage Coverage);

internal sealed class ImpactAnalysisException : Exception
{
    internal ImpactAnalysisException(string message)
        : base(message)
    {
    }
}
