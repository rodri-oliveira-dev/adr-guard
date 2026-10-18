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
        var (metadata, metadataErrors) = ParseFrontMatter(markdown);
        var sections = new List<AdrSection>();
        string? title = null;

        string? currentHeading = null;
        var currentLevel = 0;
        int? currentLine = null;
        int? currentColumn = null;
        var currentContent = new StringBuilder();
        char? fenceMarker = null;
        var fenceLength = 0;
        var firstLine = true;
        var inFrontMatter = false;
        var lineNumber = 0;

        foreach (var line in ReadLines(markdown))
        {
            lineNumber++;
            if (firstLine)
            {
                firstLine = false;
                inFrontMatter = string.Equals(line.Trim(), "---", StringComparison.Ordinal);
                if (inFrontMatter) continue;
            }
            else if (inFrontMatter)
            {
                if (string.Equals(line.Trim(), "---", StringComparison.Ordinal)) inFrontMatter = false;
                continue;
            }

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
                    FlushSection(
                        sections,
                        currentHeading,
                        currentLevel,
                        currentContent,
                        currentLine,
                        currentColumn);
                    currentHeading = null;
                    currentLevel = 0;
                    currentLine = null;
                    currentColumn = null;
                    currentContent.Clear();
                    continue;
                }

                FlushSection(
                    sections,
                    currentHeading,
                    currentLevel,
                    currentContent,
                    currentLine,
                    currentColumn);
                currentHeading = heading;
                currentLevel = level;
                currentLine = lineNumber;
                currentColumn = line.Length - line.TrimStart().Length + 1;
                currentContent.Clear();
                continue;
            }

            AppendToCurrentSection(currentHeading, currentContent, line);
        }

        FlushSection(
            sections,
            currentHeading,
            currentLevel,
            currentContent,
            currentLine,
            currentColumn);

        var sectionStatus = sections
            .FirstOrDefault(section =>
                section.Level == 2
                && string.Equals(
                    section.Heading,
                    "Status",
                    StringComparison.OrdinalIgnoreCase))
            ?.Content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        // Canonical ADRs take their status from the level-two Status section.
        // MADR-only documents without that section can still expose front matter.
        var status = sectionStatus ?? (metadata.TryGetValue("status", out var metadataStatus)
            ? metadataStatus
            : null);

        return new AdrDocument(
            filePath,
            fileName,
            id,
            slug,
            title,
            status,
            sections,
            metadata,
            id.HasValue
                ? $"ADR-{id.Value.ToString(CultureInfo.InvariantCulture)}"
                : slug is null ? null : $"slug:{slug.ToLowerInvariant()}",
            AdrDecisionMetadata.From(metadata),
            metadataErrors);
    }

    private static (Dictionary<string, string> Values, IReadOnlyList<string> Errors) ParseFrontMatter(string markdown)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        using var reader = new StringReader(markdown);

        if (!string.Equals(reader.ReadLine()?.Trim(), "---", StringComparison.Ordinal))
        {
            return (metadata, errors);
        }

        while (reader.ReadLine() is { } line)
        {
            if (string.Equals(line.Trim(), "---", StringComparison.Ordinal))
            {
                return (metadata, errors);
            }

            if (line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            var quoted = value.Length >= 2
                && ((value[0] == '"' && value[^1] == '"')
                    || (value[0] == '\'' && value[^1] == '\''));
            // Only leading, unquoted YAML indicators introduce anchors, aliases
            // or tags. Ordinary scalar content and quoted values may use these
            // characters without enabling YAML evaluation.
            var yamlIndicator = !quoted && value.Length > 0
                && value[0] is '&' or '*' or '!';
            if (key.Length > 64 || value.Length > 4096 || yamlIndicator)
            {
                errors.Add($"Metadata '{key}' exceeds limits or uses unsupported YAML features.");
                continue;
            }
            if (quoted)
            {
                value = value[1..^1];
            }

            if (!metadata.TryAdd(key, value))
                errors.Add($"Metadata key '{key}' is duplicated.");
        }

        errors.Add("Front matter is not terminated.");
        return (metadata, errors);
    }

    private static (int? Id, string? Slug) ParseFileName(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        if (stem.StartsWith("ADR-", StringComparison.OrdinalIgnoreCase))
        {
            stem = stem[4..];
        }
        var separatorIndex = stem.IndexOf('-');

        if (separatorIndex <= 0 || separatorIndex == stem.Length - 1)
        {
            return (null, null);
        }

        var idText = stem[..separatorIndex];
        var slug = stem[(separatorIndex + 1)..];

        return int.TryParse(idText, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? (id, slug)
            : IsSlug(stem) ? (null, stem) : (null, null);
    }

    private static bool IsSlug(string value) => value.Length > 0
        && value[0] != '-'
        && value[^1] != '-'
        && !value.Contains("--", StringComparison.Ordinal)
        && value.All(character => character == '-' || char.IsAsciiDigit(character) || character is >= 'a' and <= 'z');

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
        marker = default;
        markerLength = 0;
        var indentation = 0;
        while (indentation < line.Length && line[indentation] == ' ') indentation++;
        if (indentation >= 4) return false;
        var trimmed = line.AsSpan(indentation);

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
        level = 0;
        heading = string.Empty;
        var indentation = 0;
        while (indentation < line.Length && line[indentation] == ' ') indentation++;
        if (indentation >= 4) return false;
        var trimmed = line[indentation..];

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
        StringBuilder content,
        int? line,
        int? column)
    {
        if (heading is null)
        {
            return;
        }

        sections.Add(new AdrSection(
            heading,
            level,
            content.ToString().Trim(),
            line,
            column));
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
