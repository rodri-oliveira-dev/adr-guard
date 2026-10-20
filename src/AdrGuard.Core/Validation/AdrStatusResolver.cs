using AdrGuard.Model;

namespace AdrGuard.Validation;

internal static class AdrStatusResolver
{
    internal static IReadOnlyList<AdrDocument> ForFormat(
        IReadOnlyList<AdrDocument> documents,
        AdrFormat format)
    {
        ArgumentNullException.ThrowIfNull(documents);

        if (format != AdrFormat.Madr4)
        {
            return documents;
        }

        // MADR 4.0 explicitly owns its metadata status, even when an extra
        // level-two Status section is present for narrative purposes.
        return documents.Select(document =>
            document.Metadata is not null
            && document.Metadata.TryGetValue("status", out var metadataStatus)
                ? document with { Status = metadataStatus }
                : document).ToArray();
    }
}
