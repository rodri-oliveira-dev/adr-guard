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

    private static void ValidateField(
        string fieldName,
        string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return;
        }

        using var reader = new StringReader(content);
        char? fenceMarker = null;
        var fenceLength = 0;

        while (reader.ReadLine() is { } line)
        {
            if (TryGetFence(
                    line,
                    out var marker,
                    out var markerLength))
            {
                if (fenceMarker is null)
                {
                    fenceMarker = marker;
                    fenceLength = markerLength;
                    continue;
                }

                if (marker == fenceMarker
                    && markerLength >= fenceLength
                    && IsClosingFence(line, markerLength))
                {
                    fenceMarker = null;
                    fenceLength = 0;
                    continue;
                }
            }

            if (fenceMarker is not null
                || !TryParseHeading(
                    line,
                    out var level,
                    out var heading))
            {
                continue;
            }

            if (level == 1
                || (level == 2
                    && CanonicalLevelTwoHeadings.Contains(
                        heading)))
            {
                throw new InvalidOperationException(
                    $"AI provider generated structural Markdown in the {fieldName} field. "
                    + "Generated prose must not define level-one titles or canonical level-two ADR sections.");
            }
        }
    }

    private static bool TryGetFence(
        string line,
        out char marker,
        out int markerLength)
    {
        var trimmed = line.TrimStart();
        marker = default;
        markerLength = 0;

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

        return markerLength >= 3;
    }

    private static bool IsClosingFence(
        string line,
        int markerLength)
    {
        var trimmed = line.TrimStart();

        return trimmed[markerLength..]
            .All(char.IsWhiteSpace);
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
}
