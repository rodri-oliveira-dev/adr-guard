namespace AdrGuard.Parsing;

internal static class AdrFileDiscovery
{
    internal static IReadOnlyList<string> FindMarkdownFiles(string directoryPath)
        => FindMarkdownFiles(directoryPath, skipReparsePoints: false);

    internal static IReadOnlyList<string> FindMarkdownFiles(
        string directoryPath,
        bool skipReparsePoints)
        => FindMarkdownFilesCore(directoryPath, skipReparsePoints, maximumCount: null, CancellationToken.None);

    internal static IReadOnlyList<string> FindMarkdownFiles(
        string directoryPath,
        bool skipReparsePoints,
        int maximumCount,
        CancellationToken cancellationToken)
        => FindMarkdownFilesCore(directoryPath, skipReparsePoints, maximumCount, cancellationToken);

    private static string[] FindMarkdownFilesCore(
        string directoryPath,
        bool skipReparsePoints,
        int? maximumCount,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        if (maximumCount < 0) throw new ArgumentOutOfRangeException(nameof(maximumCount));

        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = skipReparsePoints ? FileAttributes.ReparsePoint : (FileAttributes)0,
            ReturnSpecialDirectories = false,
        };
        var paths = new List<string>();
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var path in Directory.EnumerateFiles(directoryPath, "*", enumerationOptions))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsAdrMarkdownFile(path)) continue;

            paths.Add(path);
            if (maximumCount.HasValue && paths.Count > maximumCount.Value) break;
        }

        return paths.Order(StringComparer.Ordinal).ToArray();
    }

    private static bool IsAdrMarkdownFile(string path) =>
        string.Equals(
            Path.GetExtension(path),
            ".md",
            StringComparison.OrdinalIgnoreCase)
        && !string.Equals(
            Path.GetFileName(path),
            "README.md",
            StringComparison.OrdinalIgnoreCase);
}
