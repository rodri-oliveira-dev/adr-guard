namespace AdrGuard.Parsing;

internal static class AdrFileDiscovery
{
    internal static IReadOnlyList<string> FindMarkdownFiles(string directoryPath)
        => FindMarkdownFiles(directoryPath, skipReparsePoints: false);

    internal static IReadOnlyList<string> FindMarkdownFiles(
        string directoryPath,
        bool skipReparsePoints)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = skipReparsePoints ? FileAttributes.ReparsePoint : (FileAttributes)0,
            ReturnSpecialDirectories = false,
        };
        return Directory
            .EnumerateFiles(directoryPath, "*", enumerationOptions)
            .Where(IsAdrMarkdownFile)
            .Order(StringComparer.Ordinal)
            .ToArray();
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
