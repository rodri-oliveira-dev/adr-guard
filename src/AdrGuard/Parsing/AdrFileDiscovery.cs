namespace AdrGuard.Parsing;

internal static class AdrFileDiscovery
{
    internal static IReadOnlyList<string> FindMarkdownFiles(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        return Directory
            .EnumerateFiles(directoryPath, "*.md", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
