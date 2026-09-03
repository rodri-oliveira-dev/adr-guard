using System.Text;

namespace AdrGuard.Generation;

internal sealed class AtomicAdrDraftFilePersistence
    : IAdrDraftFilePersistence
{
    internal const string TemporaryFileSuffix = ".tmp";

    private readonly Func<
        string,
        string,
        CancellationToken,
        Task> _temporaryFileWriter;
    private readonly Action<string, string> _promoteTemporaryFile;

    internal AtomicAdrDraftFilePersistence(
        Func<
            string,
            string,
            CancellationToken,
            Task>? temporaryFileWriter = null,
        Action<string, string>? promoteTemporaryFile = null)
    {
        _temporaryFileWriter =
            temporaryFileWriter
            ?? WriteTemporaryFileAsync;
        _promoteTemporaryFile =
            promoteTemporaryFile
            ?? PromoteTemporaryFile;
    }

    public async Task WriteNewAsync(
        string filePath,
        string content,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(content);

        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(filePath);
        var directoryPath = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException(
                $"Unable to resolve ADR directory for '{fullPath}'.");

        var temporaryPath = Path.Combine(
            directoryPath,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}{TemporaryFileSuffix}");

        try
        {
            await _temporaryFileWriter(
                    temporaryPath,
                    content,
                    cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                _promoteTemporaryFile(
                    temporaryPath,
                    fullPath);
            }
            catch (IOException exception)
                when (File.Exists(fullPath))
            {
                throw new IOException(
                    $"ADR file already exists: '{fullPath}'.",
                    exception);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static async Task WriteTemporaryFileAsync(
        string temporaryPath,
        string content,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            options: FileOptions.Asynchronous);

        await using var writer = new StreamWriter(
            stream,
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false));

        await writer
            .WriteAsync(
                content.AsMemory(),
                cancellationToken)
            .ConfigureAwait(false);

        await writer
            .FlushAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static void PromoteTemporaryFile(
        string temporaryPath,
        string filePath) =>
        File.Move(
            temporaryPath,
            filePath);
}
