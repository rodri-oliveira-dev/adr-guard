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

    internal AdrGenerationService(IAdrGenerationProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _provider = provider;
    }

    internal async Task<AdrGenerationOutcome> GenerateAsync(
        string directoryPath,
        string title,
        string context,
        string cultureName,
        IReadOnlyList<string> contextFilePaths,
        bool includeExistingAdrs,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);
        ArgumentNullException.ThrowIfNull(contextFilePaths);

        var documents = AdrDocumentLoader.LoadDirectory(directoryPath);
        var existingValidation = AdrValidator.Validate(documents);

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

        var content = BuildMarkdown(title, generated);
        var candidate = AdrMarkdownParser.Parse(filePath, content);
        var validation = AdrValidator.Validate(
            documents
                .Append(candidate)
                .ToArray());

        if (!validation.IsValid)
        {
            return new AdrGenerationOutcome(
                filePath,
                content,
                validation,
                Written: false);
        }

        await WriteNewFileAsync(filePath, content, cancellationToken)
            .ConfigureAwait(false);

        return new AdrGenerationOutcome(
            filePath,
            content,
            validation,
            Written: true);
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

    private static async Task WriteNewFileAsync(
        string filePath,
        string content,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true);
        await using var writer = new StreamWriter(stream);

        await writer.WriteAsync(content.AsMemory(), cancellationToken)
            .ConfigureAwait(false);
    }
}
