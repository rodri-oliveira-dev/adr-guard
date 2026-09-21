using AdrGuard.Model;
using AdrGuard.Parsing;
using AdrGuard.Validation;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AdrGuard.Generation;

internal sealed record AdrCreationResult(
    string FilePath,
    ValidationResult ValidationResult);

internal sealed record AdrRenderedCreationResult(
    string FilePath,
    string Content,
    ValidationResult ValidationResult);

/// <summary>
/// Shared creation boundary for AI drafts and future offline templates. A named,
/// OS-managed mutex serializes cooperating writers across processes on the same
/// host without leaving lock/reservation files in the ADR directory.
/// </summary>
internal sealed class AdrCreationService
{
    private readonly IAdrDraftFilePersistence _persistence;

    internal AdrCreationService(IAdrDraftFilePersistence persistence)
    {
        _persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
    }

    internal static string AllocateFilePath(
        string directoryPath,
        string title,
        IReadOnlyList<AdrDocument> documents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(documents);

        var slug = AdrSlug.Create(title);
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new InvalidOperationException(
                "Unable to create an ADR filename from the supplied title. "
                + "The title must contain at least one ASCII letter or digit.");
        }

        var id = AdrIdAllocator.NextId(documents);
        return Path.GetFullPath(
            Path.Combine(
                directoryPath,
                $"{id.ToString("D4", CultureInfo.InvariantCulture)}-{slug}.md"));
    }

    internal static AdrCreationResult Prepare(
        string directoryPath,
        string title,
        string content,
        IReadOnlyList<AdrDocument> documents,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();

        var filePath = AllocateFilePath(directoryPath, title, documents);
        var candidate = AdrMarkdownParser.Parse(filePath, content);

        cancellationToken.ThrowIfCancellationRequested();

        var validation = AdrValidator.Validate(
            documents.Append(candidate).ToArray());

        cancellationToken.ThrowIfCancellationRequested();
        return new AdrCreationResult(filePath, validation);
    }

    internal async Task<AdrCreationResult> PersistAsync(
        string directoryPath,
        string title,
        string content,
        string previewPath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        // Preserve the default draft contract by using its original content.
        var result = await PersistRenderedAsync(
                directoryPath,
                title,
                _ => content,
                previewPath,
                cancellationToken)
            .ConfigureAwait(false);

        return new AdrCreationResult(
            result.FilePath,
            result.ValidationResult);
    }

    /// <summary>
    /// Calls the renderer with the final allocated ID while holding the same
    /// creation mutex used by draft. No provider work may run in the callback.
    /// The callback's exact content is validated and atomically persisted.
    /// </summary>
    internal Task<AdrRenderedCreationResult> PersistRenderedAsync(
        string directoryPath,
        string title,
        Func<int, string> renderForId,
        string previewPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(previewPath);
        ArgumentNullException.ThrowIfNull(renderForId);
        cancellationToken.ThrowIfCancellationRequested();

        // Mutex ownership is thread-affine on all supported platforms.
        return Task.Run(
            () => PersistUnderLock(
                directoryPath,
                title,
                renderForId,
                previewPath,
                cancellationToken),
            CancellationToken.None);
    }

    private AdrRenderedCreationResult PersistUnderLock(
        string directoryPath,
        string title,
        Func<int, string> renderForId,
        string previewPath,
        CancellationToken cancellationToken)
    {
        using var mutex = new Mutex(
            initiallyOwned: false,
            name: CreateMutexName(directoryPath));

        var acquired = false;
        try
        {
            while (!acquired)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // WaitAny with named mutexes is unsupported on Unix.
                    // Bounded polling keeps cancellation responsive while
                    // WaitOne remains portable across supported hosts.
                    acquired = mutex.WaitOne(
                        TimeSpan.FromMilliseconds(100));
                }
                catch (AbandonedMutexException)
                {
                    // The previous writer died: the OS released the lock,
                    // and freshly loading the ADR set below is still required.
                    acquired = true;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Preserve the existing draft same-path conflict contract even if
            // an unrelated writer created the original filename during generation.
            if (File.Exists(previewPath))
            {
                throw new IOException(
                    $"ADR file already exists: '{previewPath}'.");
            }

            // The initial preview may be stale after a different-title writer
            // committed. Reallocate and validate the complete current set
            // while excluding every other cooperating creator.
            var documents = AdrDocumentLoader.LoadDirectory(
                directoryPath,
                cancellationToken);
            var existingValidation = AdrValidator.Validate(documents);

            cancellationToken.ThrowIfCancellationRequested();

            if (!existingValidation.IsValid)
            {
                throw new InvalidOperationException(
                    "The ADR directory changed and contains invalid records. "
                    + "Run 'adr-guard check' and resolve its diagnostics before retrying creation.");
            }

            var finalId = AdrIdAllocator.NextId(documents);
            var content = renderForId(finalId);
            ArgumentNullException.ThrowIfNull(content);

            var candidate = Prepare(
                directoryPath,
                title,
                content,
                documents,
                cancellationToken);

            if (!candidate.ValidationResult.IsValid)
            {
                return new AdrRenderedCreationResult(
                    candidate.FilePath,
                    content,
                    candidate.ValidationResult);
            }

            if (File.Exists(candidate.FilePath))
            {
                throw new IOException(
                    $"ADR file already exists: '{candidate.FilePath}'.");
            }

            // The existing persistence implementation uses a unique temporary
            // file, a flushed write and no-overwrite atomic promotion.
            _persistence.WriteNewAsync(
                    candidate.FilePath,
                    content,
                    cancellationToken)
                .GetAwaiter()
                .GetResult();

            return new AdrRenderedCreationResult(
                candidate.FilePath,
                content,
                candidate.ValidationResult);
        }
        finally
        {
            if (acquired)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    private static string CreateMutexName(string directoryPath)
    {
        var fullPath = Path.GetFullPath(directoryPath);
        var canonicalPath = OperatingSystem.IsWindows()
            ? fullPath.ToUpperInvariant()
            : fullPath;
        canonicalPath = Path.TrimEndingDirectorySeparator(canonicalPath);

        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(canonicalPath));

        return "adr-guard-create-" + Convert.ToHexString(hash);
    }
}
