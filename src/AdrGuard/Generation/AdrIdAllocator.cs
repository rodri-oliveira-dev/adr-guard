using AdrGuard.Model;

namespace AdrGuard.Generation;

/// <summary>
/// Deterministic, shared numeric ADR allocation. Persistence must repeat allocation
/// while holding the creation lock; a preview allocation alone never reserves an ID.
/// </summary>
internal static class AdrIdAllocator
{
    internal const int MaximumAdrId = 9999;

    internal static int NextId(IReadOnlyList<AdrDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var maximumId = documents
            .Where(document => document.Id is > 0)
            .Select(document => document.Id!.Value)
            .DefaultIfEmpty(0)
            .Max();

        if (maximumId >= MaximumAdrId)
        {
            throw new InvalidOperationException(
                $"Unable to allocate a new ADR ID because {MaximumAdrId:D4} is the maximum supported ID.");
        }

        return maximumId + 1;
    }
}
