namespace AdrGuard.Generation;

internal interface IAdrDraftFilePersistence
{
    Task WriteNewAsync(
        string filePath,
        string content,
        CancellationToken cancellationToken);
}
