using System.Text;

namespace AdrGuard.Generation;

internal static class MadrMarkdownRenderer
{
    private const string FrontMatter = "---\nstatus: \"proposed\"\n---\n\n";

    internal static string RenderTemplate(MadrTemplateRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);
        ArgumentNullException.ThrowIfNull(request.Template);
        ArgumentNullException.ThrowIfNull(request.Substitutions);

        if (request.Title.IndexOfAny(['\r', '\n', '\0']) >= 0)
        {
            throw new ArgumentException(
                "A MADR title must be a single line without NUL characters.",
                nameof(request));
        }

        ValidateTemplate(request.Template);
        var expectedFields = request.Template.Sections
            .Select(section => section.FieldId)
            .ToHashSet(StringComparer.Ordinal);
        if (request.Substitutions.Keys.Any(key => !expectedFields.Contains(key)))
        {
            throw new ArgumentException(
                "A MADR template substitution targets an unsupported field.",
                nameof(request));
        }

        var builder = new StringBuilder(FrontMatter)
            .Append("# ")
            .Append(AdrMarkdownRenderer.EscapeInlineMarkdown(request.Title.Trim()))
            .Append('\n');

        foreach (var section in request.Template.Sections)
        {
            var content = request.Substitutions.GetValueOrDefault(section.FieldId);
            if (content is null)
            {
                content = section.Guidance;
            }

            AdrGenerationContextLimits.ValidateTemplateField(section.FieldId, content);
            if (content.Contains('\0'))
            {
                throw new ArgumentException(
                    $"MADR template field '{section.FieldId}' contains a NUL character.",
                    nameof(request));
            }

            content = AdrMarkdownRenderer.NormalizeNewlines(content);
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentException(
                    $"MADR template field '{section.FieldId}' must not be empty.",
                    nameof(request));
            }

            GeneratedAdrStructureGuard.ValidateMadrTemplateContent(
                section.Heading,
                content);
            builder
                .Append('\n')
                .Append('#', section.Level)
                .Append(' ')
                .Append(section.Heading)
                .Append("\n\n")
                .Append(content.Trim())
                .Append('\n');
            AdrGenerationContextLimits.ValidateRenderedAdrLength(builder.Length);
        }

        return builder.ToString();
    }

    private static void ValidateTemplate(MadrTemplateDefinition template)
    {
        if (template.Name is not (AdrBuiltInTemplates.Minimal or AdrBuiltInTemplates.Extended))
        {
            throw new InvalidOperationException(
                $"Unsupported MADR template definition '{template.Name}'.");
        }

        if (template.CultureName is not ("en-US" or "pt-BR"))
        {
            throw new InvalidOperationException(
                $"Unsupported MADR template culture '{template.CultureName}'.");
        }

        var sections = template.Sections
            ?? throw new InvalidOperationException(
                "The MADR template definition must provide sections.");
        var expected = template.Name == AdrBuiltInTemplates.Minimal
            ? MinimalStructure
            : ExtendedStructure;
        if (sections.Count > expected.Count)
        {
            throw new InvalidOperationException(
                "The MADR template definition is malformed.");
        }

        var expectedIndex = 0;
        foreach (var section in sections)
        {
            while (expectedIndex < expected.Count
                && !Matches(section, expected[expectedIndex]))
            {
                expectedIndex++;
            }

            if (expectedIndex >= expected.Count
                || string.IsNullOrWhiteSpace(section.Guidance))
            {
                throw new InvalidOperationException(
                    "The MADR template definition is malformed.");
            }

            expectedIndex++;
        }

        foreach (var required in MinimalStructure)
        {
            if (!sections.Any(section => Matches(section, required)))
            {
                throw new InvalidOperationException(
                    "The MADR template definition is missing a required section.");
            }
        }
    }

    private static bool Matches(
        MadrTemplateSection? section,
        (int Level, string Heading, string FieldId) expected) =>
        section is not null
        && section.Level == expected.Level
        && section.Heading == expected.Heading
        && section.FieldId == expected.FieldId;

    private static IReadOnlyList<(int Level, string Heading, string FieldId)> MinimalStructure { get; } =
    [
        (2, "Context and Problem Statement", "contextAndProblemStatement"),
        (2, "Considered Options", "consideredOptions"),
        (2, "Decision Outcome", "decisionOutcome"),
    ];

    private static IReadOnlyList<(int Level, string Heading, string FieldId)> ExtendedStructure { get; } =
    [
        (2, "Context and Problem Statement", "contextAndProblemStatement"),
        (2, "Decision Drivers", "decisionDrivers"),
        (2, "Considered Options", "consideredOptions"),
        (2, "Decision Outcome", "decisionOutcome"),
        (3, "Consequences", "consequences"),
        (3, "Confirmation", "confirmation"),
        (2, "Pros and Cons of the Options", "prosAndConsOfOptions"),
        (2, "More Information", "moreInformation"),
    ];
}
