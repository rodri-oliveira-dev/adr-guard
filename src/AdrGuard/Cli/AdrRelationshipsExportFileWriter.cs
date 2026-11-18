using System.Text;

namespace AdrGuard.Cli;

internal sealed class AdrRelationshipsExportFileWriter
{
    private const string TemporaryFileSuffix = ".tmp";

    private readonly Action<string, string> temporaryFileWriter;
    private readonly Action<string, string> promoteTemporaryFile;

    internal AdrRelationshipsExportFileWriter(
        Action<string, string>? temporaryFileWriter = null,
        Action<string, string>? promoteTemporaryFile = null)
    {
        this.temporaryFileWriter = temporaryFileWriter ?? WriteTemporaryFile;
        this.promoteTemporaryFile = promoteTemporaryFile ?? PromoteTemporaryFile;
    }

    internal void Write(string filePath, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(content);

        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException($"Unable to resolve export directory for '{fullPath}'.");
        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}{TemporaryFileSuffix}");

        try
        {
            temporaryFileWriter(temporaryPath, content);
            promoteTemporaryFile(temporaryPath, fullPath);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static void WriteTemporaryFile(string path, string content)
    {
        using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }

    private static void PromoteTemporaryFile(string temporaryPath, string filePath) =>
        File.Move(temporaryPath, filePath, overwrite: true);
}
