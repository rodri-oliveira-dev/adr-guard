namespace AdrGuard.Generation;

internal static class AdrGenerationContextLimits
{
    internal const int MaximumInlineContextCharacters = 20000;

    internal const int MaximumContextFileCharacters = 50000;

    internal const int MaximumAggregateContextFileCharacters = 100000;

    internal const int MaximumComposedContextCharacters = 120000;

    internal static string NormalizeAndValidateInlineContext(
        string inlineContext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inlineContext);

        var normalized =
            AdrGenerationText.NormalizeNewLines(
                inlineContext.Trim());

        if (normalized.Length
            > MaximumInlineContextCharacters)
        {
            throw new InvalidOperationException(
                $"Inline --context exceeds the {MaximumInlineContextCharacters}-character limit.");
        }

        return normalized;
    }

    internal static void ValidateAggregateContextFileCharacters(
        long characterCount)
    {
        if (characterCount
            <= MaximumAggregateContextFileCharacters)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Explicit context files exceed the {MaximumAggregateContextFileCharacters}-character aggregate limit.");
    }

    internal static void ValidateComposedContext(
        string composedContext)
    {
        ArgumentNullException.ThrowIfNull(composedContext);

        if (composedContext.Length
            <= MaximumComposedContextCharacters)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Composed AI generation context exceeds the {MaximumComposedContextCharacters}-character limit. "
            + "Reduce --context, --context-file content, or existing ADR context.");
    }
}
