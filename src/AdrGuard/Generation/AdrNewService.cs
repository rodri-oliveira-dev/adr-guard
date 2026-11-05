using AdrGuard.Parsing;
using AdrGuard.Validation;

namespace AdrGuard.Generation;

internal sealed record AdrNewOutcome(
    string? FilePath,
    string? Content,
    ValidationResult ValidationResult,
    bool Written);

/// <summary>
/// Offline ADR generation shares allocation, validation and atomic persistence
/// with default draft. A changed ID causes a fresh render inside the lock.
/// </summary>
internal sealed class AdrNewService
{
    private readonly AdrCreationService _creation;

    internal AdrNewService(IAdrDraftFilePersistence? persistence = null)
    {
        _creation = new AdrCreationService(
            persistence ?? new AtomicAdrDraftFilePersistence());
    }

    internal Task<AdrNewOutcome> CreateAsync(
        string directoryPath,
        string title,
        AdrTemplateDefinition template,
        bool dryRun,
        CancellationToken cancellationToken,
        AdrFilenamePolicy? filenamePolicy = null,
        IReadOnlyDictionary<string, string>? substitutions = null)
    {
        ArgumentNullException.ThrowIfNull(template);

        return CreateCoreAsync(
                directoryPath,
                title,
                dryRun,
                filenamePolicy,
                AdrFormat.Canonical,
                id => AdrMarkdownRenderer.RenderTemplate(
                    new AdrTemplateRenderRequest(
                        title,
                        template,
                        substitutions ?? new Dictionary<string, string>(),
                        Id: id)),
                cancellationToken);
    }

    internal Task<AdrNewOutcome> CreateMadrAsync(
        string directoryPath,
        string title,
        MadrTemplateDefinition template,
        bool dryRun,
        CancellationToken cancellationToken,
        AdrFilenamePolicy? filenamePolicy = null,
        IReadOnlyDictionary<string, string>? substitutions = null)
    {
        ArgumentNullException.ThrowIfNull(template);

        return CreateCoreAsync(
                directoryPath,
                title,
                dryRun,
                filenamePolicy,
                AdrFormat.Madr4,
                _ => MadrMarkdownRenderer.RenderTemplate(
                    new MadrTemplateRenderRequest(
                        title,
                        template,
                        substitutions ?? new Dictionary<string, string>())),
                cancellationToken);
    }

    private async Task<AdrNewOutcome> CreateCoreAsync(
        string directoryPath,
        string title,
        bool dryRun,
        AdrFilenamePolicy? filenamePolicy,
        AdrFormat format,
        Func<int, string> renderForId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(renderForId);

        cancellationToken.ThrowIfCancellationRequested();

        if (title.IndexOfAny(['\r', '\n']) >= 0)
        {
            throw new ArgumentException(
                "ADR title must be a single line.",
                nameof(title));
        }

        var policy = filenamePolicy ?? AdrFilenamePolicy.Canonical;
        var documents = AdrDocumentLoader.LoadDirectory(
            directoryPath,
            cancellationToken)
            .Select(policy.NormalizeIdentity).ToArray();
        var existingValidation = AdrValidator.Validate(
            documents,
            null,
            new AdrValidationOptions(
                format,
                RepositoryRoot: AdrRepositoryRoot.Resolve(directoryPath),
                FilenamePolicy: filenamePolicy));

        if (!existingValidation.IsValid)
        {
            return new AdrNewOutcome(
                null,
                null,
                existingValidation,
                Written: false);
        }

        // Both the prospective path and the preview are computed without
        // modifying directories, files, index or reservation state.
        var previewPath = AdrCreationService.AllocateFilePath(
            directoryPath,
            title,
            documents,
            filenamePolicy);

        if (File.Exists(previewPath))
        {
            throw new IOException(
                $"ADR file already exists: '{previewPath}'.");
        }

        var previewId = AdrIdAllocator.NextId(documents);
        var previewContent = renderForId(previewId);
        var preview = AdrCreationService.Prepare(
            directoryPath,
            title,
            previewContent,
            documents,
            cancellationToken,
            filenamePolicy,
            format);

        if (dryRun || !preview.ValidationResult.IsValid)
        {
            return new AdrNewOutcome(
                preview.FilePath,
                previewContent,
                preview.ValidationResult,
                Written: false);
        }

        var persisted = await _creation.PersistRenderedAsync(
                directoryPath,
                title,
                renderForId,
                previewPath,
                filenamePolicy,
                cancellationToken,
                format)
            .ConfigureAwait(false);

        return new AdrNewOutcome(
            persisted.FilePath,
            persisted.Content,
            persisted.ValidationResult,
            Written: persisted.ValidationResult.IsValid);
    }
}
