using AdrGuard.Model;
using System.Globalization;
using System.Text;

namespace AdrGuard.Parsing;

internal static class AdrMarkdownParser
{
    internal static AdrDocument Parse(string filePath, string markdown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(markdown);

        var fileName = Path.GetFileName(filePath);
        var (id, slug) = ParseFileName(fileName);
        var sections = new List<AdrSection>();
        string? title = null;

        var currentHeading = default(string);
        var currentLevel = 0;
        var currentContent = new StringBuilder();
        var inFence = false;

        foreach (var line in ReadLines(markdown))
        {
            if (IsFence(line))
            {
                inFence = !inFence;

                if (currentHeading is not null)
                {
                    AppendLine(currentContent, line);
                }

                continue;
            }

            if (!inFence && TryParseHeading(line, out var level, out var heading))
            {
                if (level == 1 && title is null)
                {
                    title = heading;
                }

                if (level >= 2)
                {
                    FlushSection(sections, currentHeading, currentLevel, currentContent);
                    currentHeading = heading;
                    currentLevel = level;
                    currentContent.Clear();
                }

                continue;
            }

            if (currentHeading is not null)
            {
                AppendLine(currentContent, line);
            }
        }

        FlushSection(sections, currentHeading, currentLevel, currentContent);

        var status = sections
            .FirstOrDefault(section => string.Equals(section.Heading, "Status", StringComparison.OrdinalIgnoreCase))
            ?.Content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        return new AdrDocument(
            filePath,
            fileName,
            id,
            slug,
            title,
            status,
            sections);
    }

    private static (int? Id, string? Slug) ParseFileName(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var separatorIndex = stem.IndexOf('-', StringComparison.Ordinal);

        if (separatorIndex <= 0 || separatorIndex == stem.Length - 1)
        {
            return (null, null);
        }

        var idText = stem[..separatorIndex];
        var slug = stem[(separatorIndex + 1)..];

        return int.TryParse(idText, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? (id, slug)
            : (null, slug);
    }

    private static IEnumerable<string> ReadLines(string markdown)
    {
        using var reader = new StringReader(markdown);

        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    private static bool IsFence(string line)
    {
        var trimmed = line.TrimStart();

        return trimmed.StartsWith("```", StringComparison.Ordinal)
            || trimmed.StartsWith("~~~", StringComparison.Ordinal);
    }

    private static bool TryParseHeading(string line, out int level, out string heading)
    {
        var trimmed = line.TrimStart();
        level = 0;
        heading = string.Empty;

        while (level < trimmed.Length && trimmed[level] == '#')
        {
            level++;
        }

        if (level is < 1 or > 6
            || level >= trimmed.Length
            || !char.IsWhiteSpace(trimmed[level]))
        {
            return false;
        }

        heading = trimmed[level..]
            .Trim()
            .TrimEnd('#')
            .TrimEnd();

        return heading.Length > 0;
    }

    private static void FlushSection(
        ICollection<AdrSection> sections,
        string? heading,
        int level,
        StringBuilder content)
    {
        if (heading is null)
        {
            return;
        }

        sections.Add(new AdrSection(
            heading,
            level,
            content.ToString().Trim()));
    }

    private static void AppendLine(StringBuilder content, string line)
    {
        if (content.Length > 0)
        {
            content.AppendLine();
        }

        content.Append(line);
    }
}
