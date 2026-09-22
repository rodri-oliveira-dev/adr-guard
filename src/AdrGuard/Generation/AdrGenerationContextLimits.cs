namespace AdrGuard.Generation;

internal static class AdrGenerationContextLimits
{
    internal const int MaximumInlineContextCharacters = 20000;

    internal const int MaximumContextFileCharacters = 50000;

    internal const int MaximumAggregateContextFileCharacters = 100000;

    internal const int MaximumComposedContextCharacters = 120000;

    internal const int MaximumGeneratedFieldCharacters = 20000;

    internal const int MaximumRenderedAdrCharacters = 256 * 1024;

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

    internal static void ValidateGeneratedResult(
        AdrGenerationResult generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        ValidateGeneratedField("context", generated.Context);
        ValidateGeneratedField("decision", generated.Decision);
        ValidateGeneratedField("consequences", generated.Consequences);
    }

    internal static void ValidateRenderedAdrLength(long characterCount)
    {
        if (characterCount <= MaximumRenderedAdrCharacters)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Rendered ADR exceeds the {MaximumRenderedAdrCharacters}-character safety limit.");
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

    private static void ValidateGeneratedField(
        string fieldName,
        string? value)
    {
        if (value is null
            || value.Length <= MaximumGeneratedFieldCharacters)
        {
            return;
        }

        throw new InvalidOperationException(
            $"AI provider generated {fieldName} content exceeding the "
            + $"{MaximumGeneratedFieldCharacters}-character safety limit.");
    }
}
