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

            var content = await File
                .ReadAllTextAsync(fullPath, cancellationToken)
                .ConfigureAwait(false);

            files.Add(new ExplicitContextFile(fullPath, content));
        }

        return files;
    }
}
