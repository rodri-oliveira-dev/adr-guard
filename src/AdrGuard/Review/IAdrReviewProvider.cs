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

internal sealed class ContractAdrReviewProvider(
    Func<AdrReviewModelRequest, CancellationToken, Task<AdrReviewResult>> transport)
    : IAdrReviewProvider
{
    private readonly Func<AdrReviewModelRequest, CancellationToken, Task<AdrReviewResult>> _transport =
        transport ?? throw new ArgumentNullException(nameof(transport));

    public Task<AdrReviewResult> ReviewAsync(
        AdrReviewRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _transport(
            new AdrReviewModelRequest(
                AdrReviewContract.Version,
                AdrReviewContract.BuildInstructions(),
                request.ProviderContext),
            cancellationToken);
    }
}

internal sealed record AdrReviewModelRequest(
    string ContractVersion,
    string Instructions,
    string Input);
