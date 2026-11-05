using AdrGuard.Validation;

namespace AdrGuard.Generation;

internal sealed record AdrFormFieldDefinition(
    string Id,
    bool Required);

internal sealed record AdrFormTemplateDefinition(
    string Name,
    IReadOnlyList<AdrFormFieldDefinition> Fields,
    AdrTemplateDefinition Template);

internal sealed record MadrFormTemplateDefinition(
    string Name,
    IReadOnlyList<AdrFormFieldDefinition> Fields,
    MadrTemplateDefinition Template,
    IReadOnlyDictionary<string, string> Substitutions);

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

    internal static MadrFormTemplateDefinition GetMadr(
        string name,
        string cultureName,
        IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var fields = FieldsFor(name, AdrFormat.Madr4);
        var builtIn = MadrBuiltInTemplates.Get(name, cultureName);
        var substitutions = new Dictionary<string, string>(StringComparer.Ordinal);
        var sections = builtIn.Sections
            .Where(section =>
            {
                var formField = MadrFieldMappings[section.FieldId];
                if (!values.TryGetValue(formField, out var value)
                    || string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }

                substitutions[section.FieldId] = value;
                return true;
            })
            .ToArray();

        return new MadrFormTemplateDefinition(
            name,
            fields,
            builtIn with { Sections = sections },
            substitutions);
    }

    internal static IReadOnlyList<AdrFormFieldDefinition> Describe(
        string name,
        AdrFormat format) =>
        FieldsFor(name, format);

    private static IReadOnlyList<AdrFormFieldDefinition> FieldsFor(
        string name,
        AdrFormat format) => (format, name) switch
    {
        (AdrFormat.Canonical, AdrBuiltInTemplates.Minimal) => MinimalFields,
        (AdrFormat.Canonical, AdrBuiltInTemplates.Extended) => ExtendedFields,
        (AdrFormat.Madr4, AdrBuiltInTemplates.Minimal) => MadrMinimalFields,
        (AdrFormat.Madr4, AdrBuiltInTemplates.Extended) => MadrExtendedFields,
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

    private static IReadOnlyList<AdrFormFieldDefinition> MadrMinimalFields { get; } =
    [
        new("context", Required: true),
        new("optionsConsidered", Required: true),
        new("decision", Required: true),
    ];

    private static IReadOnlyList<AdrFormFieldDefinition> MadrExtendedFields { get; } =
    [
        new("context", Required: true),
        new("decisionDrivers", Required: false),
        new("optionsConsidered", Required: true),
        new("decision", Required: true),
        new("consequences", Required: false),
        new("references", Required: false),
    ];

    private static Dictionary<string, string> MadrFieldMappings { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["contextAndProblemStatement"] = "context",
            ["decisionDrivers"] = "decisionDrivers",
            ["consideredOptions"] = "optionsConsidered",
            ["decisionOutcome"] = "decision",
            ["consequences"] = "consequences",
            ["confirmation"] = "rationale",
            ["prosAndConsOfOptions"] = "positiveConsequences",
            ["moreInformation"] = "references",
        };

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
