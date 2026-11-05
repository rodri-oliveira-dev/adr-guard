namespace AdrGuard.Generation;

internal sealed record MadrTemplateDefinition(
    string Name,
    string CultureName,
    IReadOnlyList<MadrTemplateSection> Sections);

internal sealed record MadrTemplateSection(
    int Level,
    string Heading,
    string FieldId,
    string Guidance);

internal sealed record MadrTemplateRenderRequest(
    string Title,
    MadrTemplateDefinition Template,
    IReadOnlyDictionary<string, string> Substitutions);
