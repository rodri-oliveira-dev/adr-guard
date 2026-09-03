using System.Globalization;
using System.Text;

namespace AdrGuard.Generation;

internal static class AdrSlug
{
    internal static string Create(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var normalized = title.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        var pendingSeparator = false;

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lower = char.ToLowerInvariant(character);
            var isAllowed = lower is >= 'a' and <= 'z'
                || char.IsAsciiDigit(lower);

            if (isAllowed)
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(lower);
                pendingSeparator = false;
                continue;
            }

            pendingSeparator = builder.Length > 0;
        }

        return builder.ToString();
    }
}
