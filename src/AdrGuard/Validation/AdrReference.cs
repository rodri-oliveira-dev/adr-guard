using AdrGuard.Configuration;
using AdrGuard.Model;
using System.Text.RegularExpressions;

namespace AdrGuard.Validation;

internal enum AdrReferenceKind
{
    Document,
    Relationship,
}

internal sealed record AdrReference(
    AdrDocument Source,
    string Target,
    string ResolvedPath,
    AdrReferenceKind Kind,
    string? InvalidReason = null)
{
    private static readonly Regex MarkdownLink = new(
        "\\[[^\\]]*\\]\\(\\s*(?:<(?<angle>[^>]+)>|(?<plain>[^\\s\\)]+))(?:\\s+(?:\"[^\"]*\"|'[^']*'|\\([^\\)]*\\)))?\\s*\\)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(250));

    private static readonly string[] RelationshipHeadings =
    [
        "Superseded by",
        "Supersedes",
        "Depends on",
        "Dependencies",
    ];

    internal static IReadOnlyList<AdrReference> FindAll(
        AdrDocument document,
        string? repositoryRoot = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var references = new List<AdrReference>();
        foreach (var section in document.Sections)
        {
            var kind = section.Level == 2
                && RelationshipHeadings.Contains(section.Heading, StringComparer.OrdinalIgnoreCase)
                    ? AdrReferenceKind.Relationship
                    : AdrReferenceKind.Document;
            ExtractReferences(document, section.Content, references, kind, repositoryRoot);
        }

        return references;
    }

    internal static IReadOnlyList<AdrReference> FindInSections(
        AdrDocument document,
        params string[] headings)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(headings);
        var references = new List<AdrReference>();
        foreach (var section in document.Sections.Where(section =>
                     section.Level == 2
                     && headings.Contains(section.Heading, StringComparer.OrdinalIgnoreCase)))
        {
            ExtractReferences(document, section.Content, references, AdrReferenceKind.Relationship, null);
        }

        return references;
    }

    internal static IReadOnlyList<AdrReference> FindInStatusText(
        AdrDocument document,
        string? repositoryRoot = null)
    {
        var references = new List<AdrReference>();
        var status = document.Status?.Trim() ?? string.Empty;
        if (status.StartsWith("Superseded by ", StringComparison.OrdinalIgnoreCase))
        {
            ExtractReferences(document, status, references, AdrReferenceKind.Relationship, repositoryRoot);
        }

        return references;
    }

    private static void ExtractReferences(
        AdrDocument document,
        string content,
        List<AdrReference> references,
        AdrReferenceKind kind,
        string? repositoryRoot)
    {
        foreach (Match match in MarkdownLink.Matches(RemoveFencedCode(content)))
        {
            var target = (match.Groups["angle"].Success
                    ? match.Groups["angle"].Value
                    : match.Groups["plain"].Value).Trim();
            if (!TryNormalizeLocalMarkdownTarget(target, out var normalizedTarget))
            {
                continue;
            }

            var sourcePath = Path.GetFullPath(document.FilePath);
            var sourceDirectory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
            // Library consumers historically validate in-memory or temporary ADRs
            // without a repository root. In that compatibility path, constrain
            // resolution to the source directory. CLI callers pass the explicit
            // invocation repository root for cross-directory document links.
            var root = Path.GetFullPath(repositoryRoot ?? sourceDirectory);
            try
            {
                var candidate = Path.GetFullPath(normalizedTarget, sourceDirectory);
                var relative = Path.GetRelativePath(root, candidate);
                var resolvedPath = RepositoryPath.ResolveContained(root, relative);
                references.Add(new AdrReference(document, target, resolvedPath, kind));
            }
            catch (Exception exception) when (exception is ArgumentException or IOException)
            {
                references.Add(new AdrReference(document, target, string.Empty, kind, exception.Message));
            }
        }
    }

    private static string RemoveFencedCode(string content)
    {
        var writer = new StringWriter();
        using var reader = new StringReader(content);
        char? marker = null;
        var openingLength = 0;
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.TrimStart();
            var length = 0;
            if (trimmed.Length >= 3 && trimmed[0] is '`' or '~')
            {
                while (length < trimmed.Length && trimmed[length] == trimmed[0]) length++;
            }

            if (length >= 3)
            {
                if (marker is null)
                {
                    marker = trimmed[0];
                    openingLength = length;
                }
                else if (trimmed[0] == marker && length >= openingLength)
                {
                    marker = null;
                    openingLength = 0;
                }
                continue;
            }

            if (marker is null) writer.WriteLine(line);
        }

        return writer.ToString();
    }

    private static bool TryNormalizeLocalMarkdownTarget(string target, out string normalizedTarget)
    {
        normalizedTarget = string.Empty;
        if (string.IsNullOrWhiteSpace(target) || target.StartsWith('#')) return false;
        if (Uri.TryCreate(target, UriKind.Absolute, out var uri)
            && !string.Equals(uri.Scheme, "file", StringComparison.OrdinalIgnoreCase)) return false;

        var fragmentIndex = target.IndexOf('#');
        var queryIndex = target.IndexOf('?');
        var suffixIndex = fragmentIndex < 0 ? queryIndex : queryIndex < 0 ? fragmentIndex : Math.Min(fragmentIndex, queryIndex);
        var path = suffixIndex >= 0 ? target[..suffixIndex] : target;
        path = Uri.UnescapeDataString(path.Trim().Trim('<', '>'));
        if (!string.Equals(Path.GetExtension(path), ".md", StringComparison.OrdinalIgnoreCase)) return false;
        normalizedTarget = path;
        return true;
    }
}
