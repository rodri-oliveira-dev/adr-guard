using AdrGuard.Generation;

namespace AdrGuard.Review;

internal static class AdrReviewFallbackMapper
{
    internal static AdrReviewResult Map(AdrGenerationResult generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        var sourceText = string.Join(
            Environment.NewLine + Environment.NewLine,
            new[] { generated.Context, generated.Decision, generated.Consequences }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim()));

        if (string.IsNullOrWhiteSpace(sourceText))
        {
            throw new InvalidOperationException("The configured provider returned an empty review response.");
        }

        return new AdrReviewResult(
            AdrReviewContract.Dimensions
                .Select(dimension => new AdrReviewFinding(
                    dimension,
                    "missing-context",
                    null,
                    null,
                    "not enough information: the configured provider transport did not return the structured review contract for this dimension.",
                    $"Human reviewer should investigate {dimension}. Provider output: {sourceText}"))
                .ToArray());
    }
}
