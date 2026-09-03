namespace AdrGuard.Parsing;

internal static class AdrFileDiscovery
{
    internal static IReadOnlyList<string> FindMarkdownFiles(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        return Directory
            .EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories)
            .Where(path => string.Equals(
                Path.GetExtension(path),
                ".md",
                StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
