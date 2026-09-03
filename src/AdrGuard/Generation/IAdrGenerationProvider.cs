namespace AdrGuard.Generation;

internal interface IAdrGenerationProvider
{
    Task<AdrGenerationResult> GenerateAsync(
        AdrGenerationRequest request,
        CancellationToken cancellationToken);
}
