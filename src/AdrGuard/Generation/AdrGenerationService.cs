using AdrGuard.Model;
using AdrGuard.Parsing;
using AdrGuard.Validation;

namespace AdrGuard.Generation;

internal sealed class AdrGenerationService
{
    private readonly IAdrGenerationProvider _provider;
    private readonly AdrCreationService _creation;

    internal AdrGenerationService(
        IAdrGenerationProvider provider,
        IAdrDraftFilePersistence? persistence = null)
    {
        ArgumentNullException.ThrowIfNull(provider);

        _provider = provider;
        _creation = new AdrCreationService(
            persistence ?? new AtomicAdrDraftFilePersistence());
    }

    internal async Task<AdrGenerationOutcome> GenerateAsync(
        string directoryPath,
        string title,
        string context,
        string cultureName,
        IReadOnlyList<string> contextFilePaths,
        bool includeExistingAdrs,
        bool dryRun,
        CancellationToken cancellationToken,
        AdrTemplateDefinition? template = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);
        ArgumentNullException.ThrowIfNull(contextFilePaths);

        cancellationToken.ThrowIfCancellationRequested();

        AdrGenerationContextLimits
            .NormalizeAndValidateInlineContext(context);

        var documents = AdrDocumentLoader.LoadDirectory(
            directoryPath,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        var existingValidation = AdrValidator.Validate(documents);

        cancellationToken.ThrowIfCancellationRequested();

        if (!existingValidation.IsValid)
        {
            return new AdrGenerationOutcome(
                FilePath: null,
                Content: null,
                existingValidation,
                Written: false);
        }

        var explicitContextFiles = await ExplicitContextFileLoader
            .LoadAsync(contextFilePaths, cancellationToken)
            .ConfigureAwait(false);

        var requestContext = AdrGenerationContextBuilder.Build(
            context,
            explicitContextFiles,
            documents,
            includeExistingAdrs);

        cancellationToken.ThrowIfCancellationRequested();

        var filePath = AdrCreationService.AllocateFilePath(
            directoryPath,
            title,
            documents);

        if (File.Exists(filePath))
        {
            throw new IOException($"ADR file already exists: '{filePath}'.");
        }

        var generated = await _provider
            .GenerateAsync(
                new AdrGenerationRequest(
                    title.Trim(),
                    requestContext,
                    cultureName),
                cancellationToken)
            .ConfigureAwait(false);

        ArgumentNullException.ThrowIfNull(generated);
        GeneratedAdrStructureGuard.Validate(generated);

        cancellationToken.ThrowIfCancellationRequested();

        // The provider receives exactly the existing composed architectural context;
        // template bodies and localized guidance are rendered locally after its call.
        // Never call the provider in the shared creation critical section.
        var substitutions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["context"] = generated.Context ?? string.Empty,
            ["decision"] = generated.Decision ?? string.Empty,
            ["consequences"] = generated.Consequences ?? string.Empty,
        };

        string RenderForId(int id) => template is null
            ? AdrMarkdownRenderer.RenderDefaultDraft(title, generated)
            : AdrMarkdownRenderer.RenderTemplate(
                new AdrTemplateRenderRequest(title, template, substitutions, id));

        var previewId = AdrIdAllocator.NextId(documents);
        var content = RenderForId(previewId);
        var preview = AdrCreationService.Prepare(
            directoryPath,
            title,
            content,
            documents,
            cancellationToken);

        if (!preview.ValidationResult.IsValid || dryRun)
        {
            return new AdrGenerationOutcome(
                preview.FilePath,
                content,
                preview.ValidationResult,
                Written: false);
        }

        if (template is null)
        {
            // Preserve the unselected draft's existing rendering and persistence.
            var defaultPersisted = await _creation.PersistAsync(
                    directoryPath,
                    title,
                    content,
                    filePath,
                    cancellationToken)
                .ConfigureAwait(false);

            return new AdrGenerationOutcome(
                defaultPersisted.FilePath,
                content,
                defaultPersisted.ValidationResult,
                Written: defaultPersisted.ValidationResult.IsValid);
        }

        var persisted = await _creation.PersistRenderedAsync(
                directoryPath,
                title,
                RenderForId,
                filePath,
                cancellationToken)
            .ConfigureAwait(false);

        return new AdrGenerationOutcome(
            persisted.FilePath,
            persisted.Content,
            persisted.ValidationResult,
            Written: persisted.ValidationResult.IsValid);
    }



}
