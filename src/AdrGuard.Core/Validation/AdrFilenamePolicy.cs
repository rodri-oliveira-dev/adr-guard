using AdrGuard.Model;
using System.Globalization;

namespace AdrGuard.Validation;

internal enum AdrFilenameConvention { Canonical, Unnumbered, AdrPrefix, Numeric }

internal sealed record AdrFilenamePolicy(AdrFilenameConvention Convention, int NumericWidth = 4)
{
    internal static AdrFilenamePolicy Canonical { get; } = new(AdrFilenameConvention.Canonical, 4);

    internal static AdrFilenamePolicy Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "canonical") return Canonical;
        if (value == "unnumbered") return new(AdrFilenameConvention.Unnumbered);
        if (value == "adr-prefix") return new(AdrFilenameConvention.AdrPrefix, 4);
        if (value.StartsWith("numeric:", StringComparison.Ordinal)
            && int.TryParse(value[8..], NumberStyles.None, CultureInfo.InvariantCulture, out var width)
            && width is >= 1 and <= 9)
            return new(AdrFilenameConvention.Numeric, width);
        throw new ArgumentException("Filename policy must be canonical, unnumbered, adr-prefix, or numeric:1..9.", nameof(value));
    }

    // The unnumbered convention treats the entire stem as a slug, including
    // stems beginning with digits (for example, 2026-plan.md).
    internal AdrDocument NormalizeIdentity(AdrDocument document)
    {
        if (Convention != AdrFilenameConvention.Unnumbered) return document;

        var stem = Path.GetFileNameWithoutExtension(document.FileName);
        return document with
        {
            Id = null,
            Slug = stem,
            StableId = $"slug:{stem.ToLowerInvariant()}",
        };
    }

    internal bool IsValid(AdrDocument document)
    {
        if (!string.Equals(Path.GetExtension(document.FileName), ".md", StringComparison.Ordinal)) return false;
        var stem = Path.GetFileNameWithoutExtension(document.FileName);
        var slug = document.Slug ?? stem;
        if (string.IsNullOrWhiteSpace(slug) || !IsSlug(slug)) return false;
        return Convention switch
        {
            AdrFilenameConvention.Canonical => document.Id is > 0 and <= 9999 && document.FileName.StartsWith($"{document.Id:D4}-", StringComparison.Ordinal),
            AdrFilenameConvention.Unnumbered => IsSlug(stem),
            AdrFilenameConvention.AdrPrefix => document.Id is > 0 and <= 9999 && document.FileName.StartsWith($"ADR-{document.Id:D4}-", StringComparison.Ordinal),
            AdrFilenameConvention.Numeric => document.Id is > 0 && document.FileName.StartsWith($"{document.Id.Value.ToString($"D{NumericWidth}", CultureInfo.InvariantCulture)}-", StringComparison.Ordinal),
            _ => false,
        };
    }

    internal string Format(int id, string slug) => Convention switch
    {
        AdrFilenameConvention.Unnumbered => $"{slug}.md",
        AdrFilenameConvention.AdrPrefix => $"ADR-{id:D4}-{slug}.md",
        AdrFilenameConvention.Numeric => $"{id.ToString($"D{NumericWidth}", CultureInfo.InvariantCulture)}-{slug}.md",
        _ => $"{id:D4}-{slug}.md",
    };

    private static bool IsSlug(string value) => value.Length > 0
        && value[0] != '-'
        && value[^1] != '-'
        && !value.Contains("--", StringComparison.Ordinal)
        && value.All(character => character == '-' || char.IsAsciiDigit(character) || character is >= 'a' and <= 'z');
}
