namespace AdrGuard.Generation;

internal sealed record ExplicitContextFile(
    string FilePath,
    string Content);

internal static class ExplicitContextFileLoader
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".md",
            ".txt",
        };

    internal static async Task<IReadOnlyList<ExplicitContextFile>> LoadAsync(
        IReadOnlyList<string> filePaths,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        if (filePaths.Count == 0)
        {
            return [];
        }

        var files = new List<ExplicitContextFile>(filePaths.Count);
        var aggregateCharacterCount = 0;

        foreach (var filePath in filePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            var fullPath = Path.GetFullPath(filePath);
            var extension = Path.GetExtension(fullPath);

            if (!SupportedExtensions.Contains(extension))
            {
                throw new InvalidOperationException(
                    $"Unsupported context file extension '{extension}' for '{fullPath}'. "
                    + "Only .md and .txt files are supported.");
            }

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    $"Context file does not exist: '{fullPath}'.",
                    fullPath);
            }

            var content = await ReadBoundedAsync(
                    fullPath,
                    cancellationToken)
                .ConfigureAwait(false);

            aggregateCharacterCount += content.Length;

            AdrGenerationContextLimits
                .ValidateAggregateContextFileCharacters(
                    aggregateCharacterCount);

            files.Add(new ExplicitContextFile(
                fullPath,
                content));
        }

        return files;
    }

    private static async Task<string> ReadBoundedAsync(
        string fullPath,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        using var reader = new StreamReader(stream);

        var buffer = new char[
            AdrGenerationContextLimits
                .MaximumContextFileCharacters
            + 1];

        var totalRead = 0;

        while (totalRead < buffer.Length)
        {
            var read = await reader
                .ReadAsync(
                    buffer.AsMemory(totalRead),
                    cancellationToken)
                .ConfigureAwait(false);

            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        if (totalRead
            > AdrGenerationContextLimits
                .MaximumContextFileCharacters)
        {
            throw new InvalidOperationException(
                $"Context file '{fullPath}' exceeds the "
                + $"{AdrGenerationContextLimits.MaximumContextFileCharacters}-character per-file limit.");
        }

        return new string(buffer, 0, totalRead);
    }
}
