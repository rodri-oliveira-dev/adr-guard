namespace AdrGuard.Review.Reporting;

internal enum AdrReviewOutputFormat
{
    Text,
    Json,
}

internal sealed record AdrReviewReport(
    string SchemaVersion,
    string GeneratedAtUtc,
    AdrReviewTargetReport Target,
    AdrReviewReviewerMetadata Reviewer,
    AdrReviewProviderMetadata Provider,
    AdrReviewInputScopeReport InputScope,
    string Outcome,
    AdrReviewDimensionReport[] Dimensions,
    AdrReviewFollowUpFindingReport[] Findings,
    string[] Limitations,
    string CostCaveat);

internal sealed record AdrReviewTargetReport(
    string SourceId,
    string Path,
    string? AdrId,
    string? Title,
    string? Status);

internal sealed record AdrReviewReviewerMetadata(
    string Name,
    string ContractVersion);

internal sealed record AdrReviewProviderMetadata(
    string Name,
    string Model);

internal sealed record AdrReviewInputScopeReport(
    bool IncludeExistingAdrs,
    AdrReviewInputSourceReport[] ExplicitContext,
    AdrReviewInputSourceReport[] ExistingAdrs,
    bool ExistingAdrSelectionBounded);

internal sealed record AdrReviewInputSourceReport(
    string SourceId,
    string Path,
    string? AdrId);

internal sealed record AdrReviewDimensionReport(
    string Name,
    AdrReviewAssessmentReport[] Assessments);

internal sealed record AdrReviewAssessmentReport(
    string Classification,
    string Explanation,
    string Guidance,
    string? FollowUpPriority,
    string? Uncertainty,
    AdrReviewEvidenceReport[] Evidence);

internal sealed record AdrReviewFollowUpFindingReport(
    string Dimension,
    string Classification,
    string FollowUpPriority,
    string? Uncertainty,
    string Explanation,
    string Guidance,
    AdrReviewEvidenceReport[] Evidence);

internal sealed record AdrReviewEvidenceReport(
    string SourceId,
    string Path,
    string? AdrId,
    int? Line,
    string? Excerpt);
