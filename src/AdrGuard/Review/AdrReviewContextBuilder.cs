using AdrGuard.Generation;
using AdrGuard.Model;
using AdrGuard.Parsing;
using System.Text;

namespace AdrGuard.Review;

internal sealed record AdrReviewContext(
    string TargetSourceName,
    string TargetMarkdown,
    IReadOnlyList<ExplicitContextFile> ExplicitFiles,
    ExistingAdrContext? ExistingAdrs,
    AdrCrossAdrEvidence? CrossAdrEvidence);

internal static class AdrReviewContextBuilder
{
    internal const int MaximumPromptCharacters = 120000;

    internal static async Task<AdrReviewContext> BuildAsync(
        string targetPath,
        string targetMarkdown,
        IReadOnlyList<string> contextFilePaths,
        bool includeExistingAdrs,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(targetMarkdown);
        ArgumentNullException.ThrowIfNull(contextFilePaths);

        await AdrReviewExplicitContextValidator
            .ValidateAsync(
                contextFilePaths,
                cancellationToken)
            .ConfigureAwait(false);

        var explicitFiles = await ExplicitContextFileLoader
            .LoadAsync(
                contextFilePaths,
                cancellationToken)
            .ConfigureAwait(false);

        ExistingAdrContext? existingContext = null;
        AdrCrossAdrEvidence? crossAdrEvidence = null;

        if (includeExistingAdrs)
        {
            var directory = Path.GetDirectoryName(targetPath)
                ?? Directory.GetCurrentDirectory();
            var targetFullPath = Path.GetFullPath(targetPath);
            var targetDocument = AdrMarkdownParser.Parse(
                targetFullPath,
                targetMarkdown);

            var documents = AdrDocumentLoader
                .LoadDirectory(directory, cancellationToken)
                .Where(document =>
                    !string.Equals(
                        Path.GetFullPath(document.FilePath),
                        targetFullPath,
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();

            existingContext = ExistingAdrContextBuilder.Build(documents);

            var remainingExistingAdrBudget = Math.Max(
                0,
                ExistingAdrContextBuilder.MaximumContextCharacters
                - existingContext.Content.Length);

            crossAdrEvidence = AdrCrossAdrEvidenceBuilder.Build(
                targetDocument,
                documents,
                Math.Min(
                    AdrCrossAdrEvidenceBuilder.MaximumCharacters,
                    remainingExistingAdrBudget));
        }

        var context = new AdrReviewContext(
            Path.GetFileName(targetPath),
            targetMarkdown,
            explicitFiles,
            existingContext,
            crossAdrEvidence);

        ValidatePromptSize(context);

        return context;
    }

    internal static string ComposeProviderContext(
        AdrReviewContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var builder = new StringBuilder();
        builder.Append("Target ADR source [target]: ")
            .Append(context.TargetSourceName)
            .Append(AdrGenerationText.NewLine)
            .Append(context.TargetMarkdown.Trim());

        for (var index = 0; index < context.ExplicitFiles.Count; index++)
        {
            var file = context.ExplicitFiles[index];

            builder.Append(AdrGenerationText.DoubleNewLine)
                .Append("Explicit context source [context-")
                .Append(index + 1)
                .Append("]: ")
                .Append(Path.GetFileName(file.FilePath))
                .Append(AdrGenerationText.NewLine)
                .Append(file.Content.Trim());
        }

        if (context.ExistingAdrs is { IncludedCount: > 0 } existing)
        {
            builder.Append(AdrGenerationText.DoubleNewLine)
                .Append("Existing ADR context (parsed and bounded):")
                .Append(AdrGenerationText.NewLine)
                .Append(existing.Content);
        }

        if (context.CrossAdrEvidence is { } crossAdrEvidence)
        {
            builder.Append(AdrGenerationText.DoubleNewLine)
                .Append(crossAdrEvidence.Content);
        }

        var composed = builder.ToString();

        if (composed.Length > MaximumPromptCharacters)
        {
            throw new InvalidOperationException(
                $"Review context exceeds the {MaximumPromptCharacters}-character final prompt limit.");
        }

        return composed;
    }

    private static void ValidatePromptSize(
        AdrReviewContext context) =>
        _ = ComposeProviderContext(context);
}
