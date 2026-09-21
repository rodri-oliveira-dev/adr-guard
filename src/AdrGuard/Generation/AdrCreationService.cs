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

    internal Task<AdrCreationResult> PersistAsync(
        string directoryPath,
        string title,
        string content,
        string previewPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(previewPath);
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();

        // System.Threading.Mutex is thread-affine: both WaitOne and ReleaseMutex
        // must run on the same worker thread, including the async file write.
        // The provider call and all dry-runs happen before reaching this worker.
        return Task.Run(
            () => PersistUnderLock(
                directoryPath,
                title,
                content,
                previewPath,
                cancellationToken),
            CancellationToken.None);
    }

    private AdrCreationResult PersistUnderLock(
        string directoryPath,
        string title,
        string content,
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
                    var signaled = WaitHandle.WaitAny(
                        [mutex, cancellationToken.WaitHandle],
                        millisecondsTimeout: 100);

                    if (signaled == 0)
                    {
                        acquired = true;
                    }
                    else if (signaled == 1)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }
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

            var candidate = Prepare(
                directoryPath,
                title,
                content,
                documents,
                cancellationToken);

            if (!candidate.ValidationResult.IsValid)
            {
                return candidate;
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

            return candidate;
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
