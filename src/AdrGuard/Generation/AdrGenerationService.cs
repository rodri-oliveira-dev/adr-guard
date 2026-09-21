using AdrGuard.Model;
using AdrGuard.Validation;
using System.Text;

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
        CancellationToken cancellationToken)
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

        var content = BuildMarkdown(title, generated);
        var preview = _creation.Prepare(
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

        var persisted = await _creation.PersistAsync(
                directoryPath,
                title,
                content,
                filePath,
                cancellationToken)
            .ConfigureAwait(false);

        return new AdrGenerationOutcome(
            persisted.FilePath,
            content,
            persisted.ValidationResult,
            Written: persisted.ValidationResult.IsValid);
    }

    private static string BuildMarkdown(
        string title,
        AdrGenerationResult generated)
    {
        var builder = new StringBuilder();

        builder
            .Append("# ")
            .AppendLine(title.Trim())
            .AppendLine()
            .AppendLine("## Status")
            .AppendLine()
            .AppendLine("Proposed")
            .AppendLine()
            .AppendLine("## Context")
            .AppendLine()
            .AppendLine(generated.Context?.Trim() ?? string.Empty)
            .AppendLine()
            .AppendLine("## Decision")
            .AppendLine()
            .AppendLine(generated.Decision?.Trim() ?? string.Empty)
            .AppendLine()
            .AppendLine("## Consequences")
            .AppendLine()
            .AppendLine(generated.Consequences?.Trim() ?? string.Empty);

        return builder.ToString();
    }


}
