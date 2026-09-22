using AdrGuard.Generation;
using AdrGuard.Parsing;
using AdrGuard.Validation;
using Xunit;

namespace AdrGuard.Tests.Generation;

public sealed class AdrMarkdownRendererTests
{
    [Fact]
    public void DefaultDraftKeepsExactLegacyWhitespaceAndOutput()
    {
        var generated = new AdrGenerationResult(
            "  Need a cache.  ",
            "  Use Redis.  ",
            "  Operate Redis.  ");
        var newline = Environment.NewLine;
        var expected = string.Join(newline,
        [
            "# Use Redis",
            "",
            "## Status",
            "",
            "Proposed",
            "",
            "## Context",
            "",
            "Need a cache.",
            "",
            "## Decision",
            "",
            "Use Redis.",
            "",
            "## Consequences",
            "",
            "Operate Redis.",
            "",
        ]);

        Assert.Equal(
            expected,
            AdrMarkdownRenderer.RenderDefaultDraft("  Use Redis  ", generated));
    }

    [Fact]
    public void DefaultDraftDoesNotConstrainCurrentProviderCultures()
    {
        var markdown = AdrMarkdownRenderer.RenderDefaultDraft(
            "Gebruik Redis",
            new AdrGenerationResult("Context.", "Decision.", "Consequences."));

        Assert.Contains("## Context", markdown, StringComparison.Ordinal);
        Assert.Contains("## Decision", markdown, StringComparison.Ordinal);
        Assert.Contains("Proposed", markdown, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("en-US", "Describe the context and constraints.", "Describe the decision and its rationale.")]
    [InlineData("pt-BR", "Descreva o contexto e as restrições.", "Descreva a decisão e sua justificativa.")]
    public void TemplateGuidanceIsLocalizedWithoutChangingCanonicalStructure(
        string culture,
        string contextGuidance,
        string decisionGuidance)
    {
        var markdown = AdrMarkdownRenderer.RenderTemplate(
            Request(culture));

        Assert.Contains(contextGuidance, markdown, StringComparison.Ordinal);
        Assert.Contains(decisionGuidance, markdown, StringComparison.Ordinal);
        Assert.Contains("## Status\n\nProposed\n", markdown, StringComparison.Ordinal);
        Assert.Contains("## Context\n", markdown, StringComparison.Ordinal);
        Assert.Contains("## Decision\n", markdown, StringComparison.Ordinal);
        Assert.Contains("## Consequences\n", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", markdown, StringComparison.Ordinal);
        Assert.True(AdrValidator.Validate(
            [AdrMarkdownParser.Parse("0001-use-redis.md", markdown)]).IsValid);
    }

    [Fact]
    public void RepeatedRenderingAndDifferentInputNewlinesYieldIdenticalLfMarkdown()
    {
        var first = AdrMarkdownRenderer.RenderTemplate(
            Request("pt-BR", context: "First line\r\nSecond line\rThird line"));
        var second = AdrMarkdownRenderer.RenderTemplate(
            Request("pt-BR", context: "First line\nSecond line\nThird line"));

        Assert.Equal(first, second);
        Assert.Equal(
            first,
            AdrMarkdownRenderer.RenderTemplate(
                Request("pt-BR", context: "First line\r\nSecond line\rThird line")));
        Assert.Contains("First line\nSecond line\nThird line", first, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", first, StringComparison.Ordinal);
    }

    [Fact]
    public void TemplateRenderingRejectsPlaceholderAmplificationPastSafetyLimit()
    {
        var repeatedContext = string.Concat(
            Enumerable.Repeat("{{context}}\n", 20));
        var request = Request(
            "en-US",
            context: new string(
                'x',
                AdrGenerationContextLimits.MaximumGeneratedFieldCharacters),
            sectionTemplate: repeatedContext);

        var error = Assert.Throws<InvalidOperationException>(
            () => AdrMarkdownRenderer.RenderTemplate(request));

        Assert.Contains(
            AdrGenerationContextLimits.MaximumRenderedAdrCharacters.ToString(System.Globalization.CultureInfo.InvariantCulture),
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TitleMarkdownCharactersAreEscapedAndCannotCreateAdditionalHeading()
    {
        var markdown = AdrMarkdownRenderer.RenderTemplate(
            Request("en-US", title: "Use #Redis *and* [cache](now)"));

        Assert.StartsWith(
            "# Use \\#Redis \\*and\\* \\[cache\\]\\(now\\)\n",
            markdown,
            StringComparison.Ordinal);
        Assert.True(AdrValidator.Validate(
            [AdrMarkdownParser.Parse("0001-use-cache.md", markdown)]).IsValid);
    }

    [Theory]
    [InlineData("# unexpected title")]
    [InlineData("## Status\n\nAccepted")]
    [InlineData("## Context\n\nHijacked")]
    [InlineData("## Additional section")]
    [InlineData("Injected title\n================")]
    [InlineData("Decision\n--------")]
    [InlineData("    ```\n## Additional section")]
    public void SubstitutionCannotInjectStructuralHeadings(string malicious)
    {
        var request = Request("en-US", context: "Valid.\n" + malicious);

        var error = Assert.Throws<InvalidOperationException>(
            () => AdrMarkdownRenderer.RenderTemplate(request));

        Assert.Contains("level-one or level-two", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProviderMarkdownGuardsRemainCompatibleWithLegacyDraft()
    {
        var generated = new AdrGenerationResult(
            "Reason.\n## Status\nAccepted", "Decision.", "Consequences.");

        var error = Assert.Throws<InvalidOperationException>(
            () => GeneratedAdrStructureGuard.Validate(generated));

        Assert.Contains("AI provider generated structural Markdown", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LiteralPlaceholderDelimitersInSubstitutionsAreNotEvaluatedAgain()
    {
        var markdown = AdrMarkdownRenderer.RenderTemplate(
            Request("en-US", context: "Literal {{decision}} is data."));

        Assert.Contains(
            "Literal {{decision}} is data.",
            markdown,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{{undefined}}")]
    [InlineData("{{context}")]
    [InlineData("{{context")]
    [InlineData("context}}")]
    [InlineData("{{{{context}}")]
    public void UnknownOrMalformedTemplatePlaceholdersFailDeterministically(
        string templateContent)
    {
        var request = Request(
            "en-US",
            sectionTemplate: templateContent);

        Assert.Throws<InvalidOperationException>(
            () => AdrMarkdownRenderer.RenderTemplate(request));
    }

    [Fact]
    public void ReservedOrUnrecognizedSubstitutionsAreRejected()
    {
        var template = Definition("en-US");
        var invalid = new AdrTemplateRenderRequest(
            "Use Redis",
            template,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["guidance-context"] = "Override the built-in guidance.",
            });

        var error = Assert.Throws<ArgumentException>(
            () => AdrMarkdownRenderer.RenderTemplate(invalid));

        Assert.Contains("reserved", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "guidance-context",
            AdrMarkdownRenderer.SupportedPlaceholders);
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("pt-PT")]
    public void UnsupportedTemplateGuidanceCultureFailsWithoutAffectingDefaultDraft(
        string culture)
    {
        var error = Assert.Throws<ArgumentException>(
            () => AdrMarkdownRenderer.RenderTemplate(Request(culture)));

        Assert.Contains("en-US", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingCanonicalSectionAndDuplicateOrNonCanonicalHeadingsFail()
    {
        var baseSections = Definition("en-US").Sections;

        Assert.Throws<InvalidOperationException>(
            () => AdrMarkdownRenderer.RenderTemplate(
                new AdrTemplateRenderRequest(
                    "Use Redis",
                    new AdrTemplateDefinition("en-US", baseSections.Skip(1).ToArray()),
                    new Dictionary<string, string>())));

        Assert.Throws<InvalidOperationException>(
            () => AdrMarkdownRenderer.RenderTemplate(
                new AdrTemplateRenderRequest(
                    "Use Redis",
                    new AdrTemplateDefinition(
                        "en-US",
                        baseSections.Append(new AdrTemplateSection("Status", "Accepted")).ToArray()),
                    new Dictionary<string, string>())));

        Assert.Throws<InvalidOperationException>(
            () => AdrMarkdownRenderer.RenderTemplate(
                new AdrTemplateRenderRequest(
                    "Use Redis",
                    new AdrTemplateDefinition(
                        "en-US",
                        baseSections.Append(new AdrTemplateSection("context", "Hijacked")).ToArray()),
                    new Dictionary<string, string>())));
    }

    [Fact]
    public void SupplementalSectionsCannotOverrideCanonicalSections()
    {
        var template = Definition("pt-BR");
        var sections = template.Sections.Append(
            new AdrTemplateSection(
                "Alternativas consideradas",
                "As opções avaliadas foram registradas.")).ToArray();

        var markdown = AdrMarkdownRenderer.RenderTemplate(
            new AdrTemplateRenderRequest(
                "Usar Redis",
                new AdrTemplateDefinition("pt-BR", sections),
                new Dictionary<string, string>()));

        Assert.Contains("## Alternativas consideradas", markdown, StringComparison.Ordinal);
        Assert.True(AdrValidator.Validate(
            [AdrMarkdownParser.Parse("0001-usar-redis.md", markdown)]).IsValid);
    }

    private static AdrTemplateRenderRequest Request(
        string culture,
        string title = "Use Redis",
        string context = "The service needs caching.",
        string? sectionTemplate = null)
    {
        var sections = Definition(culture).Sections.ToArray();
        if (sectionTemplate is not null)
        {
            sections[0] = new AdrTemplateSection("Context", sectionTemplate);
        }

        return new AdrTemplateRenderRequest(
            title,
            new AdrTemplateDefinition(culture, sections),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["context"] = context,
                ["decision"] = "Use Redis.",
                ["consequences"] = "Operate Redis.",
            });
    }

    private static AdrTemplateDefinition Definition(string culture) =>
        new(
            culture,
            [
                new AdrTemplateSection(
                    "Context",
                    "{{guidance-context}}\n\n{{context}}"),
                new AdrTemplateSection(
                    "Decision",
                    "{{guidance-decision}}\n\n{{decision}}"),
                new AdrTemplateSection(
                    "Consequences",
                    "{{guidance-consequences}}\n\n{{consequences}}"),
            ]);
}
