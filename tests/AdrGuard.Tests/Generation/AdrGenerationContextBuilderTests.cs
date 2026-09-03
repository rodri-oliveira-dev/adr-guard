using AdrGuard.Generation;
using AdrGuard.Model;
using System.Globalization;
using Xunit;

namespace AdrGuard.Tests.Generation;

public sealed class AdrGenerationContextBuilderTests
{
    [Fact]
    public void BuildWithoutAdditionalSourcesPreservesInlineOnlyContext()
    {
        var result = AdrGenerationContextBuilder.Build(
            "  Inline context.  ",
            [],
            [],
            includeExistingAdrs: false);

        Assert.Equal("Inline context.", result);
    }

    [Fact]
    public void BuildAcceptsInlineContextExactlyAtLimit()
    {
        var inlineContext = new string(
            'x',
            AdrGenerationContextLimits
                .MaximumInlineContextCharacters);

        var result = AdrGenerationContextBuilder.Build(
            inlineContext,
            [],
            [],
            includeExistingAdrs: false);

        Assert.Equal(inlineContext, result);
    }

    [Fact]
    public void BuildRejectsInlineContextOneCharacterOverLimit()
    {
        var inlineContext = new string(
            'x',
            AdrGenerationContextLimits
                .MaximumInlineContextCharacters
            + 1);

        var exception = Assert.Throws<InvalidOperationException>(
            () => AdrGenerationContextBuilder.Build(
                inlineContext,
                [],
                [],
                includeExistingAdrs: false));

        Assert.Contains(
            "Inline --context",
            exception.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            AdrGenerationContextLimits
                .MaximumInlineContextCharacters
                .ToString(CultureInfo.InvariantCulture),
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BuildRejectsComposedContextOverFinalLimit()
    {
        var inlineContext = new string(
            'i',
            AdrGenerationContextLimits
                .MaximumInlineContextCharacters);

        var contextFiles = new[]
        {
            new ExplicitContextFile(
                Path.Combine(Path.GetTempPath(), "first.txt"),
                new string(
                    'a',
                    AdrGenerationContextLimits
                        .MaximumContextFileCharacters)),
            new ExplicitContextFile(
                Path.Combine(Path.GetTempPath(), "second.txt"),
                new string(
                    'b',
                    AdrGenerationContextLimits
                        .MaximumContextFileCharacters)),
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => AdrGenerationContextBuilder.Build(
                inlineContext,
                contextFiles,
                [],
                includeExistingAdrs: false));

        Assert.Contains(
            "Composed AI generation context",
            exception.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            AdrGenerationContextLimits
                .MaximumComposedContextCharacters
                .ToString(CultureInfo.InvariantCulture),
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BuildCombinesExplicitFilesBeforeExistingAdrContext()
    {
        var contextFilePath = Path.Combine(
            Path.GetTempPath(),
            "architecture",
            "constraints.md");

        var contextFiles = new[]
        {
            new ExplicitContextFile(
                contextFilePath,
                "Use managed infrastructure where practical."),
        };

        var document = new AdrDocument(
            Path.Combine(Path.GetTempPath(), "0001-use-postgresql.md"),
            "0001-use-postgresql.md",
            1,
            "use-postgresql",
            "Use PostgreSQL",
            "Accepted",
            [
                new AdrSection("Status", 2, "Accepted"),
                new AdrSection("Context", 2, "Relational persistence is required."),
                new AdrSection("Decision", 2, "Use PostgreSQL."),
                new AdrSection("Consequences", 2, "Operate PostgreSQL."),
            ]);

        var result = AdrGenerationContextBuilder.Build(
            "Inline context.",
            contextFiles,
            [document],
            includeExistingAdrs: true);

        var fileIndex = result.IndexOf(
            "Context file 1 (constraints.md):",
            StringComparison.Ordinal);
        var adrIndex = result.IndexOf(
            "Existing ADR context:",
            StringComparison.Ordinal);

        Assert.True(fileIndex >= 0);
        Assert.True(adrIndex > fileIndex);
        Assert.Contains(
            "Use managed infrastructure where practical.",
            result,
            StringComparison.Ordinal);
        Assert.Contains("ADR 0001", result, StringComparison.Ordinal);
        Assert.DoesNotContain(
            Path.GetFullPath(contextFilePath),
            result,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BuildNormalizesSourceLineEndingsToLf()
    {
        var contextFiles = new[]
        {
            new ExplicitContextFile(
                Path.Combine(
                    Path.GetTempPath(),
                    "context.md"),
                "File line one.\r\nFile line two.\rFile line three."),
        };

        var result = AdrGenerationContextBuilder.Build(
            "Inline line one.\r\nInline line two.",
            contextFiles,
            [],
            includeExistingAdrs: false);

        Assert.DoesNotContain(
            "\r",
            result,
            StringComparison.Ordinal);
        Assert.Contains(
            "Inline line one.\nInline line two.",
            result,
            StringComparison.Ordinal);
        Assert.Contains(
            "File line one.\nFile line two.\nFile line three.",
            result,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BuildWithExistingAdrOptInButNoExistingAdrsPreservesInlineOnlyContext()
    {
        var result = AdrGenerationContextBuilder.Build(
            "Inline context.",
            [],
            [],
            includeExistingAdrs: true);

        Assert.Equal("Inline context.", result);
    }
}
