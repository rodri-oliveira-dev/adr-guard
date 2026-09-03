using AdrGuard.Model;

namespace AdrGuard.Validation;

internal sealed record AdrReference(
    AdrDocument Source,
    string Target,
    string ResolvedPath)
{
    internal static IReadOnlyList<AdrReference> FindAll(AdrDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var references = new List<AdrReference>();

        foreach (var section in document.Sections)
        {
            ExtractReferences(document, section.Content, references);
        }

        return references;
    }

    private static void ExtractReferences(
        AdrDocument document,
        string content,
        List<AdrReference> references)
    {
        var searchIndex = 0;

        while (searchIndex < content.Length)
        {
            var targetStart = content.IndexOf("](", searchIndex, StringComparison.Ordinal);
            if (targetStart < 0)
            {
                return;
            }

            targetStart += 2;
            var targetEnd = content.IndexOf(')', targetStart);
            if (targetEnd < 0)
            {
                return;
            }

            var target = content[targetStart..targetEnd].Trim();
            searchIndex = targetEnd + 1;

            if (!TryNormalizeLocalMarkdownTarget(target, out var normalizedTarget))
            {
                continue;
            }

            var sourceDirectory = Path.GetDirectoryName(document.FilePath) ?? string.Empty;
            var resolvedPath = Path.GetFullPath(Path.Combine(sourceDirectory, normalizedTarget));

            references.Add(new AdrReference(document, target, resolvedPath));
        }
    }

    private static bool TryNormalizeLocalMarkdownTarget(
        string target,
        out string normalizedTarget)
    {
        normalizedTarget = string.Empty;

        if (string.IsNullOrWhiteSpace(target)
            || target.StartsWith('#')
            || Uri.TryCreate(target, UriKind.Absolute, out _))
        {
            return false;
        }

        var fragmentIndex = target.IndexOf('#');
        var queryIndex = target.IndexOf('?');
        var suffixIndex = MinPositive(fragmentIndex, queryIndex);
        var path = suffixIndex >= 0 ? target[..suffixIndex] : target;

        path = path.Trim().Trim('<', '>');

        if (!string.Equals(Path.GetExtension(path), ".md", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        normalizedTarget = path;
        return true;
    }

    private static int MinPositive(int first, int second)
    {
        if (first < 0)
        {
            return second;
        }

        if (second < 0)
        {
            return first;
        }

        return Math.Min(first, second);
    }
}
