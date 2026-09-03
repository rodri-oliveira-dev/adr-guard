using AdrGuard.Model;
using System.Globalization;
using System.Text;

namespace AdrGuard.Generation;

internal static class AdrGenerationContextBuilder
{
    internal static string Build(
        string inlineContext,
        IReadOnlyList<ExplicitContextFile> contextFiles,
        IReadOnlyList<AdrDocument> documents,
        bool includeExistingAdrs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            inlineContext);
        ArgumentNullException.ThrowIfNull(contextFiles);
        ArgumentNullException.ThrowIfNull(documents);

        var normalizedInlineContext =
            AdrGenerationText.NormalizeNewLines(
                inlineContext.Trim());

        var existingContext = includeExistingAdrs
            ? ExistingAdrContextBuilder.Build(documents)
            : null;

        if (contextFiles.Count == 0
            && (existingContext is null
                || existingContext.IncludedCount == 0))
        {
            return normalizedInlineContext;
        }

        var builder = new StringBuilder();

        builder
            .Append(
                "User-supplied architectural context:")
            .Append(AdrGenerationText.NewLine)
            .Append(normalizedInlineContext);

        if (contextFiles.Count > 0)
        {
            builder
                .Append(
                    AdrGenerationText.DoubleNewLine)
                .Append("Explicit context files:")
                .Append(AdrGenerationText.NewLine);

            for (var index = 0;
                 index < contextFiles.Count;
                 index++)
            {
                var contextFile = contextFiles[index];

                if (index > 0)
                {
                    builder.Append(
                        AdrGenerationText.DoubleNewLine);
                }

                builder
                    .Append("Context file ")
                    .Append(
                        (index + 1).ToString(
                            CultureInfo.InvariantCulture))
                    .Append(" (")
                    .Append(
                        Path.GetFileName(
                            contextFile.FilePath))
                    .Append("):")
                    .Append(AdrGenerationText.NewLine)
                    .Append(
                        AdrGenerationText.NormalizeNewLines(
                            contextFile.Content.Trim()));
            }
        }

        if (existingContext is { IncludedCount: > 0 })
        {
            builder
                .Append(
                    AdrGenerationText.DoubleNewLine)
                .Append("Existing ADR context:")
                .Append(AdrGenerationText.NewLine)
                .Append(existingContext.Content);

            if (existingContext.IsBounded)
            {
                builder
                    .Append(
                        AdrGenerationText.DoubleNewLine)
                    .Append(
                        "Existing ADR context was bounded deterministically to ")
                    .Append(
                        existingContext.IncludedCount.ToString(
                            CultureInfo.InvariantCulture))
                    .Append(" of ")
                    .Append(
                        existingContext.TotalCount.ToString(
                            CultureInfo.InvariantCulture))
                    .Append(" ADRs using a ")
                    .Append(
                        ExistingAdrContextBuilder
                            .MaximumContextCharacters
                            .ToString(
                                CultureInfo.InvariantCulture))
                    .Append("-character limit.");
            }
        }

        return builder.ToString();
    }
}
