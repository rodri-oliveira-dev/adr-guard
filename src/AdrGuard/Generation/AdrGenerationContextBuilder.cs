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
        ArgumentException.ThrowIfNullOrWhiteSpace(inlineContext);
        ArgumentNullException.ThrowIfNull(contextFiles);
        ArgumentNullException.ThrowIfNull(documents);

        var existingContext = includeExistingAdrs
            ? ExistingAdrContextBuilder.Build(documents)
            : null;

        if (contextFiles.Count == 0
            && (existingContext is null || existingContext.IncludedCount == 0))
        {
            return inlineContext.Trim();
        }

        var builder = new StringBuilder();

        builder
            .AppendLine("User-supplied architectural context:")
            .Append(inlineContext.Trim());

        if (contextFiles.Count > 0)
        {
            builder
                .AppendLine()
                .AppendLine()
                .AppendLine("Explicit context files:");

            for (var index = 0; index < contextFiles.Count; index++)
            {
                var contextFile = contextFiles[index];

                if (index > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine();
                }

                builder
                    .Append("Context file ")
                    .Append((index + 1).ToString(CultureInfo.InvariantCulture))
                    .Append(" (")
                    .Append(Path.GetFileName(contextFile.FilePath))
                    .AppendLine("):")
                    .Append(contextFile.Content.Trim());
            }
        }

        if (existingContext is { IncludedCount: > 0 })
        {
                builder
                    .AppendLine()
                    .AppendLine()
                    .AppendLine("Existing ADR context:")
                    .Append(existingContext.Content);

            if (existingContext.IsBounded)
            {
                builder
                    .AppendLine()
                    .AppendLine()
                    .Append("Existing ADR context was bounded deterministically to ")
                    .Append(existingContext.IncludedCount.ToString(CultureInfo.InvariantCulture))
                    .Append(" of ")
                    .Append(existingContext.TotalCount.ToString(CultureInfo.InvariantCulture))
                    .Append(" ADRs using a ")
                    .Append(ExistingAdrContextBuilder.MaximumContextCharacters.ToString(CultureInfo.InvariantCulture))
                    .Append("-character limit.");
            }
        }

        return builder.ToString();
    }
}
