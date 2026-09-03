using AdrGuard.Model;
using AdrGuard.Parsing;
using AdrGuard.Validation;
using System.Globalization;
using System.Text;

namespace AdrGuard.Generation;

internal sealed class AdrGenerationService
{
    private const int MaximumAdrId = 9999;

    private readonly IAdrGenerationProvider _provider;
    private readonly IAdrDraftFilePersistence _persistence;

    internal AdrGenerationService(
        IAdrGenerationProvider provider,
        IAdrDraftFilePersistence? persistence = null)
    {
        ArgumentNullException.ThrowIfNull(provider);

        _provider = provider;
        _persistence =
            persistence
            ?? new AtomicAdrDraftFilePersistence();
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

        var id = GetNextId(documents);
        var slug = AdrSlug.Create(title);

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new InvalidOperationException(
                "Unable to create an ADR filename from the supplied title. "
                + "The title must contain at least one ASCII letter or digit.");
        }

        var fileName = $"{id.ToString("D4", CultureInfo.InvariantCulture)}-{slug}.md";
        var filePath = Path.GetFullPath(Path.Combine(directoryPath, fileName));

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
        var candidate = AdrMarkdownParser.Parse(filePath, content);

        cancellationToken.ThrowIfCancellationRequested();

        var validation = AdrValidator.Validate(
            documents
                .Append(candidate)
                .ToArray());

        cancellationToken.ThrowIfCancellationRequested();

        if (!validation.IsValid)
        {
            return new AdrGenerationOutcome(
                filePath,
                content,
                validation,
                Written: false);
        }

        if (!dryRun)
        {
            await _persistence
                .WriteNewAsync(
                    filePath,
                    content,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return new AdrGenerationOutcome(
            filePath,
            content,
            validation,
            Written: !dryRun);
    }

    private static int GetNextId(IReadOnlyList<AdrDocument> documents)
    {
        var maximumId = documents
            .Where(document => document.Id is > 0)
            .Select(document => document.Id!.Value)
            .DefaultIfEmpty(0)
            .Max();

        if (maximumId >= MaximumAdrId)
        {
            throw new InvalidOperationException(
                $"Unable to allocate a new ADR ID because {MaximumAdrId:D4} is the maximum supported ID.");
        }

        return maximumId + 1;
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
