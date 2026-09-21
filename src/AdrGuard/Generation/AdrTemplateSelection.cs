namespace AdrGuard.Generation;

/// <summary>
/// Reusable command boundary for selecting exactly one template source.
/// Consumers translate ArgumentException to CLI usage errors; file and template
/// format failures are operational errors before any ADR is persisted.
/// </summary>
internal static class AdrTemplateSelection
{
    internal static AdrTemplateDefinition Resolve(
        string? builtInName,
        string? templateFilePath,
        string cultureName,
        string? invocationDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);
        cancellationToken.ThrowIfCancellationRequested();

        if (templateFilePath is not null)
        {
            if (string.IsNullOrWhiteSpace(templateFilePath))
            {
                throw new ArgumentException(
                    "--template-file requires a non-empty local Markdown path.",
                    nameof(templateFilePath));
            }

            if (builtInName is not null)
            {
                throw new ArgumentException(
                    "--template and --template-file are mutually exclusive. Select one template source.");
            }

            return AdrCustomTemplateLoader.Load(
                templateFilePath,
                cultureName,
                invocationDirectory,
                cancellationToken);
        }

        return AdrBuiltInTemplates.Get(
            builtInName,
            cultureName);
    }
}
