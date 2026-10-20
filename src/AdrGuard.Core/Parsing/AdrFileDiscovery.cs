namespace AdrGuard.Parsing;

internal static class AdrFileDiscovery
{
    internal static IReadOnlyList<string> FindMarkdownFiles(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        return Directory
            .EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories)
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
