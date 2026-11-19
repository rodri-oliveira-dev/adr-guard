using System.Text;
using AdrGuard.Configuration;
using AdrGuard.Model;

namespace AdrGuard.Parsing;

internal static class AdrDocumentLoader
{
    internal const int MaximumBoundedAdrBytes = 524_288;
    internal const int MaximumBoundedAdrFiles = 1000;

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

    internal static IReadOnlyList<AdrDocument> LoadBoundedDirectory(
        string directoryPath,
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        var root = Path.GetFullPath(repositoryRoot);
        var paths = AdrFileDiscovery.FindMarkdownFiles(
            directoryPath,
            skipReparsePoints: true,
            MaximumBoundedAdrFiles,
            cancellationToken);
        if (paths.Count > MaximumBoundedAdrFiles)
        {
            throw new InvalidDataException(
                $"ADR directory exceeds the {MaximumBoundedAdrFiles}-file input limit.");
        }

        var documents = new List<AdrDocument>(paths.Count);
        var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        foreach (var discoveredPath in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(root, Path.GetFullPath(discoveredPath));
            var filePath = RepositoryPath.ResolveContained(root, relativePath, allowMissingLeaf: false);
            var bytes = ReadBounded(filePath, relativePath);

            cancellationToken.ThrowIfCancellationRequested();
            var preambleLength = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble)
                ? Encoding.UTF8.Preamble.Length
                : 0;
            documents.Add(AdrMarkdownParser.Parse(
                filePath,
                strictUtf8.GetString(bytes.AsSpan(preambleLength))));
        }

        return documents;
    }

    private static byte[] ReadBounded(string filePath, string relativePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaximumBoundedAdrBytes)
        {
            throw Oversized(relativePath);
        }

        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1)
        {
            throw Oversized(relativePath);
        }

        return bytes;
    }

    private static InvalidDataException Oversized(string relativePath) =>
        new(
            $"ADR '{relativePath.Replace(Path.DirectorySeparatorChar, '/')}' exceeds "
            + $"the {MaximumBoundedAdrBytes}-byte input limit.");
}
