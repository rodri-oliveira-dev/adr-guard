namespace AdrGuard.Model;

internal sealed record AdrSection(
    string Heading,
    int Level,
    string Content,
    int? Line = null,
    int? Column = null);
