using AdrGuard.Generation;
using AdrGuard.Parsing;
using AdrGuard.Validation;
using Xunit;

namespace AdrGuard.Tests.Generation;

public sealed class AdrBuiltInTemplatesTests
{
    [Theory]
    [InlineData("minimal", "en-US", "Adopt Redis")]
    [InlineData("minimal", "pt-BR", "Adotar Redis")]
    [InlineData("extended", "en-US", "Adopt Redis")]
    [InlineData("extended", "pt-BR", "Adotar Redis")]
    public void BuiltInTemplateMatchesApprovedSnapshotAndPassesValidator(
        string templateName,
        string cultureName,
        string title)
    {
        var definition = AdrBuiltInTemplates.Get(templateName, cultureName);
        var request = new AdrTemplateRenderRequest(
            title,
            definition,
            new Dictionary<string, string>());

        var markdown = AdrMarkdownRenderer.RenderTemplate(request);
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Templates",
            $"{templateName}.{cultureName}.md");
        var snapshot = File.ReadAllText(fixturePath);

        Assert.Equal(snapshot, markdown);
        Assert.Equal(markdown, AdrMarkdownRenderer.RenderTemplate(request));
        Assert.DoesNotContain("\r", markdown, StringComparison.Ordinal);

        var document = AdrMarkdownParser.Parse(
            "0001-adopt-redis.md",
            markdown);

        Assert.Equal("Proposed", document.Status);
        Assert.True(AdrValidator.Validate([document]).IsValid);
        Assert.Equal(
            ["Status", "Context", "Decision", "Consequences"],
            document.Sections
                .Where(section => IsCanonical(section.Heading))
                .Select(section => section.Heading)
                .ToArray());
        Assert.Contains(
            cultureName == "pt-BR" ? "[EDITAR:" : "[EDIT:",
            markdown,
            StringComparison.Ordinal);
        Assert.Contains(
            cultureName == "pt-BR" ? "não uma decisão arquitetural aprovada" : "not an approved architectural decision",
            markdown,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultTemplateIsMinimalAndStableNamesAreCaseSensitive()
    {
        Assert.Equal(
            ["minimal", "extended"],
            AdrBuiltInTemplates.Names);

        var defaultTemplate = AdrBuiltInTemplates.Get(
            null,
            "en-US");
        var explicitMinimal = AdrBuiltInTemplates.Get(
            "minimal",
            "en-US");

        Assert.Equal(
            AdrMarkdownRenderer.RenderTemplate(
                new AdrTemplateRenderRequest(
                    "Adopt Redis",
                    defaultTemplate,
                    new Dictionary<string, string>())),
            AdrMarkdownRenderer.RenderTemplate(
                new AdrTemplateRenderRequest(
                    "Adopt Redis",
                    explicitMinimal,
                    new Dictionary<string, string>())));

        Assert.False(AdrBuiltInTemplates.TryGet(
            "Minimal",
            "en-US",
            out var ignored));
        Assert.Null(ignored);
        Assert.False(AdrBuiltInTemplates.TryGet(
            "",
            "en-US",
            out ignored));
        Assert.Null(ignored);

        var exception = Assert.Throws<ArgumentException>(
            () => AdrBuiltInTemplates.Get("unknown", "en-US"));
        Assert.Contains(
            "Use 'minimal' or 'extended'",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("minimal", "en-US")]
    [InlineData("minimal", "pt-BR")]
    [InlineData("extended", "en-US")]
    [InlineData("extended", "pt-BR")]
    public void TemplatesPreserveCanonicalHeadingsAndMeaningfulExtendedDepth(
        string templateName,
        string cultureName)
    {
        var definition = AdrBuiltInTemplates.Get(
            templateName,
            cultureName);
        var headings = definition.Sections
            .Select(section => section.Heading)
            .ToArray();

        Assert.Contains("Context", headings);
        Assert.Contains("Decision", headings);
        Assert.Contains("Consequences", headings);
        Assert.DoesNotContain("Status", headings);

        var expected = templateName == "extended"
            ? new[]
            {
                "Context",
                "Decision Drivers",
                "Options Considered",
                "Decision",
                "Rationale",
                "Consequences",
                "Positive Consequences",
                "Negative Consequences",
                "Risks",
                "References",
            }
            : ["Context", "Decision", "Consequences"];

        Assert.Equal(expected, headings);
        Assert.All(
            definition.Sections,
            section => Assert.False(
                string.IsNullOrWhiteSpace(section.BodyTemplate)));
    }

    [Fact]
    public void UnsupportedTemplateCultureFailsWithActionableError()
    {
        var error = Assert.Throws<ArgumentException>(
            () => AdrBuiltInTemplates.Get("minimal", "fr-FR"));

        Assert.Contains("en-US", error.Message, StringComparison.Ordinal);
        Assert.Contains("pt-BR", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("minimal", "en-US")]
    [InlineData("minimal", "pt-BR")]
    [InlineData("extended", "en-US")]
    [InlineData("extended", "pt-BR")]
    public void AuthorSubstitutionsCannotIntroduceDuplicateCanonicalHeadings(
        string templateName,
        string cultureName)
    {
        var template = AdrBuiltInTemplates.Get(
            templateName,
            cultureName);
        var request = new AdrTemplateRenderRequest(
            "Adopt Redis",
            template,
            new Dictionary<string, string>
            {
                ["context"] = "Problem.\n## Decision\nInjected decision.",
            });

        var error = Assert.Throws<InvalidOperationException>(
            () => AdrMarkdownRenderer.RenderTemplate(request));

        Assert.Contains(
            "level-one or level-two",
            error.Message,
            StringComparison.Ordinal);
    }

    private static bool IsCanonical(string heading) =>
        heading is "Status" or "Context" or "Decision" or "Consequences";
}
