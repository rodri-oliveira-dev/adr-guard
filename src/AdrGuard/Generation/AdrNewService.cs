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

    internal async Task<AdrNewOutcome> CreateAsync(
        string directoryPath,
        string title,
        AdrTemplateDefinition template,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(template);

        cancellationToken.ThrowIfCancellationRequested();

        if (title.IndexOfAny(['\r', '\n']) >= 0)
        {
            throw new ArgumentException(
                "ADR title must be a single line.",
                nameof(title));
        }

        var documents = AdrDocumentLoader.LoadDirectory(
            directoryPath,
            cancellationToken);
        var existingValidation = AdrValidator.Validate(documents);

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
            documents);

        if (File.Exists(previewPath))
        {
            throw new IOException(
                $"ADR file already exists: '{previewPath}'.");
        }

        string RenderForId(int id) =>
            AdrMarkdownRenderer.RenderTemplate(
                new AdrTemplateRenderRequest(
                    title,
                    template,
                    new Dictionary<string, string>(),
                    Id: id));

        var previewId = AdrIdAllocator.NextId(documents);
        var previewContent = RenderForId(previewId);
        var preview = AdrCreationService.Prepare(
            directoryPath,
            title,
            previewContent,
            documents,
            cancellationToken);

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
                RenderForId,
                previewPath,
                cancellationToken)
            .ConfigureAwait(false);

        return new AdrNewOutcome(
            persisted.FilePath,
            persisted.Content,
            persisted.ValidationResult,
            Written: persisted.ValidationResult.IsValid);
    }
}
