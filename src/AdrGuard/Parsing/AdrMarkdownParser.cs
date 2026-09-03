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

        string? currentHeading = null;
        var currentLevel = 0;
        var currentContent = new StringBuilder();
        char? fenceMarker = null;
        var fenceLength = 0;

        foreach (var line in ReadLines(markdown))
        {
            if (TryGetFence(line, out var marker, out var markerLength))
            {
                if (fenceMarker is null)
                {
                    fenceMarker = marker;
                    fenceLength = markerLength;
                    AppendToCurrentSection(currentHeading, currentContent, line);
                    continue;
                }

                if (marker == fenceMarker
                    && markerLength >= fenceLength
                    && IsClosingFence(line, markerLength))
                {
                    AppendToCurrentSection(currentHeading, currentContent, line);
                    fenceMarker = null;
                    fenceLength = 0;
                    continue;
                }
            }

            if (fenceMarker is null && TryParseHeading(line, out var level, out var heading))
            {
                if (level == 1)
                {
                    title ??= heading;
                    FlushSection(sections, currentHeading, currentLevel, currentContent);
                    currentHeading = null;
                    currentLevel = 0;
                    currentContent.Clear();
                    continue;
                }

                FlushSection(sections, currentHeading, currentLevel, currentContent);
                currentHeading = heading;
                currentLevel = level;
                currentContent.Clear();
                continue;
            }

            AppendToCurrentSection(currentHeading, currentContent, line);
        }

        FlushSection(sections, currentHeading, currentLevel, currentContent);

        var status = sections
            .FirstOrDefault(section =>
                section.Level == 2
                && string.Equals(
                    section.Heading,
                    "Status",
                    StringComparison.OrdinalIgnoreCase))
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
        var separatorIndex = stem.IndexOf('-');

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

    private static bool TryGetFence(string line, out char marker, out int markerLength)
    {
        var trimmed = line.TrimStart();
        marker = default;
        markerLength = 0;

        if (trimmed.Length < 3 || trimmed[0] is not ('`' or '~'))
        {
            return false;
        }

        marker = trimmed[0];

        while (markerLength < trimmed.Length && trimmed[markerLength] == marker)
        {
            markerLength++;
        }

        return markerLength >= 3;
    }

    private static bool IsClosingFence(string line, int markerLength)
    {
        var trimmed = line.TrimStart();
        return trimmed[markerLength..].All(char.IsWhiteSpace);
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

        heading = TrimClosingSequence(trimmed[level..].Trim());
        return heading.Length > 0;
    }

    private static string TrimClosingSequence(string heading)
    {
        var closingStart = heading.Length;

        while (closingStart > 0 && heading[closingStart - 1] == '#')
        {
            closingStart--;
        }

        if (closingStart == heading.Length
            || closingStart == 0
            || !char.IsWhiteSpace(heading[closingStart - 1]))
        {
            return heading;
        }

        return heading[..closingStart].TrimEnd();
    }

    private static void FlushSection(
        List<AdrSection> sections,
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

    private static void AppendToCurrentSection(
        string? currentHeading,
        StringBuilder content,
        string line)
    {
        if (currentHeading is null)
        {
            return;
        }

        if (content.Length > 0)
        {
            content.AppendLine();
        }

        content.Append(line);
    }
}
