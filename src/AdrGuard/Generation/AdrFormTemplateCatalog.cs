namespace AdrGuard.Generation;

internal sealed record AdrFormFieldDefinition(
    string Id,
    bool Required);

internal sealed record AdrFormTemplateDefinition(
    string Name,
    IReadOnlyList<AdrFormFieldDefinition> Fields,
    AdrTemplateDefinition Template);

/// <summary>
/// Defines the structured fields supported by the visual creation contract.
/// It deliberately supports only built-in templates: arbitrary custom template
/// bodies cannot be mapped to fields without guessing their author's intent.
/// </summary>
internal static class AdrFormTemplateCatalog
{
    internal static AdrFormTemplateDefinition Get(
        string name,
        string cultureName,
        IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var fields = name switch
        {
            AdrBuiltInTemplates.Minimal => MinimalFields,
            AdrBuiltInTemplates.Extended => ExtendedFields,
            _ => throw new ArgumentException(
                $"Unsupported visual ADR template '{name}'. Use 'minimal' or 'extended'.",
                nameof(name)),
        };

        var sections = new List<AdrTemplateSection>
        {
            new("Context", "{{context}}"),
        };

        if (name == AdrBuiltInTemplates.Extended)
        {
            AddOptional(sections, values, "decisionDrivers", "Decision Drivers");
            AddOptional(sections, values, "optionsConsidered", "Options Considered");
        }

        sections.Add(new AdrTemplateSection("Decision", "{{decision}}"));

        if (name == AdrBuiltInTemplates.Extended)
        {
            AddOptional(sections, values, "rationale", "Rationale");
        }

        sections.Add(new AdrTemplateSection("Consequences", "{{consequences}}"));

        if (name == AdrBuiltInTemplates.Extended)
        {
            AddOptional(sections, values, "positiveConsequences", "Positive Consequences");
            AddOptional(sections, values, "negativeConsequences", "Negative Consequences");
            AddOptional(sections, values, "risks", "Risks");
            AddOptional(sections, values, "references", "References");
        }

        return new AdrFormTemplateDefinition(
            name,
            fields,
            new AdrTemplateDefinition(cultureName, sections));
    }

    internal static IReadOnlyList<AdrFormFieldDefinition> Describe(string name) => name switch
    {
        AdrBuiltInTemplates.Minimal => MinimalFields,
        AdrBuiltInTemplates.Extended => ExtendedFields,
        _ => throw new ArgumentException(
            $"Unsupported visual ADR template '{name}'. Use 'minimal' or 'extended'.",
            nameof(name)),
    };

    private static IReadOnlyList<AdrFormFieldDefinition> MinimalFields { get; } =
    [
        new("context", Required: true),
        new("decision", Required: true),
        new("consequences", Required: true),
    ];

    private static IReadOnlyList<AdrFormFieldDefinition> ExtendedFields { get; } =
    [
        new("context", Required: true),
        new("decisionDrivers", Required: false),
        new("optionsConsidered", Required: false),
        new("decision", Required: true),
        new("rationale", Required: false),
        new("consequences", Required: true),
        new("positiveConsequences", Required: false),
        new("negativeConsequences", Required: false),
        new("risks", Required: false),
        new("references", Required: false),
    ];

    private static void AddOptional(
        List<AdrTemplateSection> sections,
        IReadOnlyDictionary<string, string> values,
        string id,
        string heading)
    {
        if (values.TryGetValue(id, out var value)
            && !string.IsNullOrWhiteSpace(value))
        {
            sections.Add(new AdrTemplateSection(heading, $"{{{{{id}}}}}"));
        }
    }
}
