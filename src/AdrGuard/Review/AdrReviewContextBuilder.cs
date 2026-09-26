using AdrGuard.Generation;
using AdrGuard.Model;
using AdrGuard.Parsing;
using System.Text;

namespace AdrGuard.Review;

internal sealed record AdrReviewContext(
    string TargetMarkdown,
    IReadOnlyList<ExplicitContextFile> ExplicitFiles,
    ExistingAdrContext? ExistingAdrs);

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

        var explicitFiles = await ExplicitContextFileLoader
            .LoadAsync(contextFilePaths, cancellationToken)
            .ConfigureAwait(false);

        ExistingAdrContext? existingContext = null;

        if (includeExistingAdrs)
        {
            var directory = Path.GetDirectoryName(targetPath)
                ?? Directory.GetCurrentDirectory();
            var targetFullPath = Path.GetFullPath(targetPath);

            var documents = AdrDocumentLoader
                .LoadDirectory(directory, cancellationToken)
                .Where(document =>
                    !string.Equals(
                        Path.GetFullPath(document.FilePath),
                        targetFullPath,
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();

            existingContext = ExistingAdrContextBuilder.Build(documents);
        }

        ValidatePromptSize(targetMarkdown, explicitFiles, existingContext);

        return new AdrReviewContext(
            targetMarkdown,
            explicitFiles,
            existingContext);
    }

    internal static string ComposeProviderContext(
        AdrReviewContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var builder = new StringBuilder();
        builder.Append("Target ADR:")
            .Append(AdrGenerationText.NewLine)
            .Append(context.TargetMarkdown.Trim());

        foreach (var file in context.ExplicitFiles)
        {
            builder.Append(AdrGenerationText.DoubleNewLine)
                .Append("Explicit context source: ")
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

        var composed = builder.ToString();

        if (composed.Length > MaximumPromptCharacters)
        {
            throw new InvalidOperationException(
                $"Review context exceeds the {MaximumPromptCharacters}-character final prompt limit.");
        }

        return composed;
    }

    private static void ValidatePromptSize(
        string targetMarkdown,
        IReadOnlyList<ExplicitContextFile> explicitFiles,
        ExistingAdrContext? existingContext)
    {
        var context = new AdrReviewContext(
            targetMarkdown,
            explicitFiles,
            existingContext);

        _ = ComposeProviderContext(context);
    }
}
