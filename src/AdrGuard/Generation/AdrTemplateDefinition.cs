namespace AdrGuard.Generation;

/// <summary>
/// A template is structural data. The renderer owns the H1, the canonical
/// Status/Proposed section, and all level-two section headings. Substitutions
/// are explicit text values; no script, expression or recursive expansion runs.
/// </summary>
internal sealed record AdrTemplateDefinition(
    string CultureName,
    IReadOnlyList<AdrTemplateSection> Sections);

internal sealed record AdrTemplateSection(
    string Heading,
    string BodyTemplate);

internal sealed record AdrTemplateRenderRequest(
    string Title,
    AdrTemplateDefinition Template,
    IReadOnlyDictionary<string, string> Substitutions);
