namespace AdrGuard.Parsing;

internal static class MarkdownListMarker
{
    internal static bool TryConsume(
        ReadOnlySpan<char> line,
        out int contentOffset)
    {
        contentOffset = 0;

        if (line.Length >= 2
            && line[0] is '-' or '+' or '*'
            && char.IsWhiteSpace(line[1]))
        {
            contentOffset = 2;
        }
        else
        {
            var digitCount = 0;
            while (digitCount < line.Length
                && digitCount < 9
                && char.IsAsciiDigit(line[digitCount]))
            {
                digitCount++;
            }

            if (digitCount == 0
                || digitCount + 1 >= line.Length
                || line[digitCount] is not ('.' or ')')
                || !char.IsWhiteSpace(line[digitCount + 1]))
            {
                return false;
            }

            contentOffset = digitCount + 2;
        }

        while (contentOffset < line.Length
            && char.IsWhiteSpace(line[contentOffset]))
        {
            contentOffset++;
        }

        return true;
    }
}
