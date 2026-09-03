using AdrGuard.Model;

namespace AdrGuard.Parsing;

internal static class AdrDocumentLoader
{
    internal static IReadOnlyList<AdrDocument> LoadDirectory(
        string directoryPath) =>
        LoadDirectory(
            directoryPath,
            default);

    internal static IReadOnlyList<AdrDocument> LoadDirectory(
        string directoryPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        var documents = new List<AdrDocument>();

        foreach (var filePath in AdrFileDiscovery.FindMarkdownFiles(
                     directoryPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var markdown = File.ReadAllText(filePath);

            cancellationToken.ThrowIfCancellationRequested();

            documents.Add(
                AdrMarkdownParser.Parse(
                    filePath,
                    markdown));
        }

        return documents;
    }
}
