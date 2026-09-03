using AdrGuard.Model;

namespace AdrGuard.Parsing;

internal static class AdrDocumentLoader
{
    internal static IReadOnlyList<AdrDocument> LoadDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        return AdrFileDiscovery
            .FindMarkdownFiles(directoryPath)
            .Select(filePath => AdrMarkdownParser.Parse(
                filePath,
                File.ReadAllText(filePath)))
            .ToArray();
    }
}
