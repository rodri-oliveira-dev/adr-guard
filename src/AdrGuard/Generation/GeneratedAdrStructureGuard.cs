namespace AdrGuard.Generation;

internal static class GeneratedAdrStructureGuard
{
    private static readonly HashSet<string> CanonicalLevelTwoHeadings =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Status",
            "Context",
            "Decision",
            "Consequences",
        };

    internal static void Validate(AdrGenerationResult generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        ValidateField("context", generated.Context);
        ValidateField("decision", generated.Decision);
        ValidateField("consequences", generated.Consequences);
    }

    // Template content is data, not a way to introduce structural sections.
    // The default AI draft keeps its existing, narrower structural contract.
    internal static void ValidateTemplateContent(
        string sectionName,
        string? content) =>
        ValidateField(sectionName, content, rejectAllHeadings: true);

    internal static void ValidateMadrTemplateContent(
        string sectionName,
        string? content) =>
        ValidateField(
            sectionName,
            content,
            rejectAllHeadings: true,
            maximumRejectedHeadingLevel: 3,
            rejectUnclosedFence: true);

    private static void ValidateField(
        string fieldName,
        string? content,
        bool rejectAllHeadings = false,
        int maximumRejectedHeadingLevel = 2,
        bool rejectUnclosedFence = false)
    {
        if (string.IsNullOrEmpty(content))
        {
            return;
        }

        using var reader = new StringReader(content);
        var fenceTracker = new MarkdownFenceTracker();
        string? previousUnfencedLine = null;

        while (reader.ReadLine() is { } line)
        {
            if (fenceTracker.TryConsume(line))
            {
                previousUnfencedLine = null;
                continue;
            }

            if (fenceTracker.IsInsideFence)
            {
                continue;
            }

            if (TryParseStructuralHeading(
                    line,
                    previousUnfencedLine,
                    out var level,
                    out var heading))
            {
                RejectStructuralHeading(
                    fieldName,
                    rejectAllHeadings,
                    level,
                    heading,
                    maximumRejectedHeadingLevel);
                previousUnfencedLine = null;
                continue;
            }

            previousUnfencedLine = string.IsNullOrWhiteSpace(line)
                ? null
                : line;
        }

        if (rejectUnclosedFence && fenceTracker.IsInsideFence)
        {
            throw new InvalidOperationException(
                $"Template content in the {fieldName} section must close every Markdown code fence.");
        }
    }

    private static bool TryParseStructuralHeading(
        string line,
        string? previousUnfencedLine,
        out int level,
        out string heading) =>
        TryParseHeading(
            line,
            out level,
            out heading)
        || TryParseSetextHeading(
            previousUnfencedLine,
            line,
            out level,
            out heading);

    private static void RejectStructuralHeading(
        string fieldName,
        bool rejectAllHeadings,
        int level,
        string heading,
        int maximumRejectedHeadingLevel)
    {
        if (level != 1
            && (level != 2
                || (!rejectAllHeadings
                    && !CanonicalLevelTwoHeadings.Contains(heading)))
            && (!rejectAllHeadings
                || level > maximumRejectedHeadingLevel))
        {
            return;
        }

        if (rejectAllHeadings)
        {
            var levels = maximumRejectedHeadingLevel == 2
                ? "level-one or level-two"
                : $"level-one through level-{maximumRejectedHeadingLevel}";
            throw new InvalidOperationException(
                $"Template content in the {fieldName} section must not define "
                + $"{levels} Markdown headings.");
        }

        throw new InvalidOperationException(
            $"AI provider generated structural Markdown in the {fieldName} field. "
            + "Generated prose must not define level-one titles or canonical level-two ADR sections.");
    }

    private static bool TryParseSetextHeading(
        string? previousLine,
        string line,
        out int level,
        out string heading)
    {
        level = 0;
        heading = string.Empty;

        if (string.IsNullOrWhiteSpace(previousLine))
        {
            return false;
        }

        var indentation = line.Length - line.TrimStart().Length;
        if (indentation > 3)
        {
            return false;
        }

        var underline = line.Trim();
        if (underline.Length == 0
            || underline[0] is not ('=' or '-')
            || underline.Any(character => character != underline[0]))
        {
            return false;
        }

        level = underline[0] == '=' ? 1 : 2;
        heading = previousLine.Trim();
        return heading.Length > 0;
    }

    private static bool TryParseHeading(
        string line,
        out int level,
        out string heading)
    {
        var trimmed = line.TrimStart();
        level = 0;
        heading = string.Empty;

        while (level < trimmed.Length
            && trimmed[level] == '#')
        {
            level++;
        }

        if (level is < 1 or > 6
            || level >= trimmed.Length
            || !char.IsWhiteSpace(trimmed[level]))
        {
            return false;
        }

        heading = TrimClosingSequence(
            trimmed[level..].Trim());

        return heading.Length > 0;
    }

    private static string TrimClosingSequence(
        string heading)
    {
        var closingStart = heading.Length;

        while (closingStart > 0
            && heading[closingStart - 1] == '#')
        {
            closingStart--;
        }

        if (closingStart == heading.Length
            || closingStart == 0
            || !char.IsWhiteSpace(
                heading[closingStart - 1]))
        {
            return heading;
        }

        return heading[..closingStart].TrimEnd();
    }

    private sealed class MarkdownFenceTracker
    {
        private char? marker;
        private int length;

        internal bool IsInsideFence => marker is not null;

        internal bool TryConsume(string line)
        {
            if (!TryGetFence(
                    line,
                    out var candidateMarker,
                    out var candidateLength))
            {
                return false;
            }

            if (marker is null)
            {
                marker = candidateMarker;
                length = candidateLength;
                return true;
            }

            if (candidateMarker != marker
                || candidateLength < length
                || !IsClosingFence(line, candidateLength))
            {
                return false;
            }

            marker = null;
            length = 0;
            return true;
        }

        private static bool TryGetFence(
            string line,
            out char marker,
            out int markerLength)
        {
            marker = default;
            markerLength = 0;

            var indentation = 0;
            while (indentation < line.Length
                && line[indentation] == ' ')
            {
                indentation++;
            }

            // CommonMark fenced code blocks may be indented by at most three
            // spaces. Four or more spaces are indented code, not a fence.
            if (indentation > 3)
            {
                return false;
            }

            var trimmed = line.AsSpan(indentation);
            if (trimmed.Length < 3
                || trimmed[0] is not ('`' or '~'))
            {
                return false;
            }

            marker = trimmed[0];

            while (markerLength < trimmed.Length
                && trimmed[markerLength] == marker)
            {
                markerLength++;
            }

            if (markerLength < 3)
            {
                return false;
            }

            // CommonMark forbids backticks in the info string of a backtick
            // fence. If present, this line is ordinary text and must not
            // suppress structural heading parsing on following lines.
            return marker != '`'
                || trimmed[markerLength..].IndexOf('`') < 0;
        }

        private static bool IsClosingFence(
            string line,
            int markerLength)
        {
            var trimmed = line.TrimStart();

            return trimmed[markerLength..]
                .All(char.IsWhiteSpace);
        }
    }
}
