using System.Text;
using AdrGuard.Generation;
using AdrGuard.Parsing;
using AdrGuard.Validation;
using Xunit;

namespace AdrGuard.Tests.Generation;

public sealed class MadrMarkdownRendererTests
{
    [Fact]
    public void MinimalTemplateHasDeterministicFrontMatterGuidanceAndValidStructure()
    {
        var markdown = Render("Adopt Redis", "minimal", "en-US");
        const string expectedWithoutFinalNewline = """
            ---
            status: "proposed"
            ---

            # Adopt Redis

            ## Context and Problem Statement

            [EDIT: Replace this guidance with the context, problem, constraints, and scope of the proposed decision.]

            ## Considered Options

            [EDIT: List the viable options, including keeping the current solution when applicable.]

            ## Decision Outcome

            [EDIT: Replace this guidance with the proposed outcome and why it best addresses the problem. This draft is not an approved decision.]
            """;
        var expected = expectedWithoutFinalNewline + "\n";

        Assert.Equal(expected, markdown);
        Assert.DoesNotContain('\r', markdown);
        Assert.EndsWith("\n", markdown, StringComparison.Ordinal);

        var document = AdrMarkdownParser.Parse("0001-adopt-redis.md", markdown);
        Assert.Equal("proposed", document.Status);
        Assert.True(AdrValidator.Validate([document], AdrFormat.Madr4).IsValid);
    }

    [Theory]
    [InlineData("en-US", "[EDIT:")]
    [InlineData("pt-BR", "[EDITAR:")]
    public void ExtendedTemplateUsesMadrHierarchyAndLocalizedEditableGuidance(
        string cultureName,
        string editMarker)
    {
        var markdown = Render("Adopt Redis", "extended", cultureName);
        var document = AdrMarkdownParser.Parse("0001-adopt-redis.md", markdown);

        Assert.Equal(
            [
                (2, "Context and Problem Statement"),
                (2, "Decision Drivers"),
                (2, "Considered Options"),
                (2, "Decision Outcome"),
                (3, "Consequences"),
                (3, "Confirmation"),
                (2, "Pros and Cons of the Options"),
                (2, "More Information"),
            ],
            document.Sections.Select(section => (section.Level, section.Heading)).ToArray());
        Assert.Contains(editMarker, markdown, StringComparison.Ordinal);
        Assert.True(AdrValidator.Validate([document], AdrFormat.Madr4).IsValid);
    }

    [Fact]
    public void TitleAndFieldContentPreserveUnicodeAndEscapeOnlyInlineMarkdownStructure()
    {
        var substitutions = new Dictionary<string, string>
        {
            ["contextAndProblemStatement"] = "O serviço precisa preservar café, ação e 漢字.\r\n\r\nTexto adicional.",
            ["consideredOptions"] = "* Redis\n* Memória local",
            ["decisionOutcome"] = "Propomos Redis; a aprovação continua humana.",
        };

        var markdown = MadrMarkdownRenderer.RenderTemplate(
            new MadrTemplateRenderRequest(
                "Escolher café [v2] #1",
                MadrBuiltInTemplates.Get("minimal", "pt-BR"),
                substitutions));

        Assert.Contains("# Escolher café \\[v2\\] \\#1\n", markdown, StringComparison.Ordinal);
        Assert.Contains("café, ação e 漢字.\n\nTexto adicional.", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', markdown);
        Assert.True(AdrValidator.Validate(
            [AdrMarkdownParser.Parse("0001-escolher-cafe.md", markdown)],
            AdrFormat.Madr4).IsValid);
    }

    [Theory]
    [InlineData("Title\n## Decision Outcome")]
    [InlineData("Title\0hidden")]
    public void InvalidTitlesAreRejected(string title)
    {
        Assert.Throws<ArgumentException>(
            () => Render(title, "minimal", "en-US"));
    }

    [Theory]
    [InlineData("contextAndProblemStatement", "## Decision Outcome\nInjected")]
    [InlineData("decisionOutcome", "### Consequences\nInjected")]
    [InlineData("consideredOptions", "Option\n---")]
    public void StructuralContentInjectionIsRejected(
        string fieldId,
        string content)
    {
        var substitutions = RequiredSubstitutions();
        substitutions[fieldId] = content;

        var error = Assert.Throws<InvalidOperationException>(
            () => Render("Adopt Redis", "minimal", "en-US", substitutions));

        Assert.Contains("Markdown headings", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("```text\nunclosed")]
    [InlineData("~~~text\nunclosed")]
    public void UnclosedCodeFencesAreRejectedPerMadrField(string content)
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => Render(
                "Adopt Redis",
                "minimal",
                "en-US",
                RequiredSubstitutions(content)));

        Assert.Contains("close every Markdown code fence", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ClosedCodeFencesRemainValidMadrFieldContent()
    {
        var markdown = Render(
            "Adopt Redis",
            "minimal",
            "en-US",
            RequiredSubstitutions("```markdown\n## Example heading\n```"));

        Assert.Contains("```markdown\n## Example heading\n```", markdown, StringComparison.Ordinal);
        Assert.True(AdrValidator.Validate(
            [AdrMarkdownParser.Parse("0001-adopt-redis.md", markdown)],
            AdrFormat.Madr4).IsValid);
    }

    [Fact]
    public void UnsupportedEmptyNulAndOversizedFieldsAreRejected()
    {
        Assert.Throws<ArgumentException>(
            () => Render(
                "Adopt Redis",
                "minimal",
                "en-US",
                new Dictionary<string, string> { ["status"] = "accepted" }));
        Assert.Throws<ArgumentException>(
            () => Render(
                "Adopt Redis",
                "minimal",
                "en-US",
                RequiredSubstitutions("   ")));
        Assert.Throws<ArgumentException>(
            () => Render(
                "Adopt Redis",
                "minimal",
                "en-US",
                RequiredSubstitutions("Context\0hidden")));
        Assert.Throws<InvalidOperationException>(
            () => Render(
                "Adopt Redis",
                "minimal",
                "en-US",
                RequiredSubstitutions(
                    new string('x', AdrGenerationContextLimits.MaximumGeneratedFieldCharacters + 1))));
    }

    [Fact]
    public async Task MadrCreationReusesNumberingValidationAndUtf8AtomicPersistence()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "adr-guard-madr-renderer-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var outcome = await new AdrNewService().CreateMadrAsync(
                root,
                "Adotar cache",
                MadrBuiltInTemplates.Get("minimal", "pt-BR"),
                dryRun: false,
                TestContext.Current.CancellationToken,
                substitutions: new Dictionary<string, string>
                {
                    ["contextAndProblemStatement"] = "O serviço precisa reduzir latência.",
                    ["consideredOptions"] = "* Redis\n* Cache local",
                    ["decisionOutcome"] = "Propomos Redis pelos limites de consistência.",
                });

            var path = Assert.IsType<string>(outcome.FilePath);
            Assert.True(outcome.Written);
            Assert.Equal("0001-adotar-cache.md", Path.GetFileName(path));
            var bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
            Assert.False(bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
            var markdown = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true).GetString(bytes);
            Assert.Equal(outcome.Content, markdown);
            Assert.Contains("serviço", markdown, StringComparison.Ordinal);
            Assert.True(AdrValidator.Validate(
                AdrDocumentLoader.LoadDirectory(root, TestContext.Current.CancellationToken),
                AdrFormat.Madr4).IsValid);
            Assert.Single(Directory.GetFiles(root, "*.md"));
            Assert.DoesNotContain(
                Directory.EnumerateFiles(root),
                file => file.EndsWith(AtomicAdrDraftFilePersistence.TemporaryFileSuffix, StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string Render(
        string title,
        string templateName,
        string cultureName,
        IReadOnlyDictionary<string, string>? substitutions = null) =>
        MadrMarkdownRenderer.RenderTemplate(
            new MadrTemplateRenderRequest(
                title,
                MadrBuiltInTemplates.Get(templateName, cultureName),
                substitutions ?? new Dictionary<string, string>()));

    private static Dictionary<string, string> RequiredSubstitutions(
        string context = "Context.") =>
        new(StringComparer.Ordinal)
        {
            ["contextAndProblemStatement"] = context,
            ["consideredOptions"] = "* Redis",
            ["decisionOutcome"] = "Propose Redis.",
        };
}
