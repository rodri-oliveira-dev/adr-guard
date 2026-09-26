namespace AdrGuard.Review;

internal interface IAdrReviewProvider
{
    Task<AdrReviewResult> ReviewAsync(
        AdrReviewRequest request,
        CancellationToken cancellationToken);
}

internal sealed record AdrReviewRequest(
    string TargetPath,
    string Markdown,
    string ProviderContext);
