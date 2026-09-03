namespace AdrGuard.Model;

internal sealed record AdrDocument(
    string FilePath,
    string FileName,
    int? Id,
    string? Slug,
    string? Title,
    string? Status,
    IReadOnlyList<AdrSection> Sections);
