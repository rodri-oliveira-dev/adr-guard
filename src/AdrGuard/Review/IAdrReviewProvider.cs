using AdrGuard.Generation;

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

internal sealed record AdrReviewResult(
    string Summary);

internal sealed class GenerationBackedAdrReviewProvider(
    IAdrGenerationProvider generationProvider) : IAdrReviewProvider
{
    private readonly IAdrGenerationProvider _generationProvider =
        generationProvider ?? throw new ArgumentNullException(nameof(generationProvider));

    public async Task<AdrReviewResult> ReviewAsync(
        AdrReviewRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var generationRequest = new AdrGenerationRequest(
            Path.GetFileNameWithoutExtension(request.TargetPath),
            """
            You are performing a technical review of an existing Architecture Decision Record.
            Analyze only the supplied ADR text. Do not approve, reject, rewrite, or modify the ADR.
            Return a concise review summary suitable for human assessment.

            Review inputs:
            """ + Environment.NewLine + request.ProviderContext,
            "en-US");

        var result = await _generationProvider
            .GenerateAsync(generationRequest, cancellationToken)
            .ConfigureAwait(false);

        var sections = new[]
        {
            result.Context,
            result.Decision,
            result.Consequences,
        }
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value!.Trim())
        .ToArray();

        if (sections.Length == 0)
        {
            throw new InvalidOperationException(
                "The configured provider returned an empty review response.");
        }

        return new AdrReviewResult(string.Join(
            Environment.NewLine + Environment.NewLine,
            sections));
    }
}
