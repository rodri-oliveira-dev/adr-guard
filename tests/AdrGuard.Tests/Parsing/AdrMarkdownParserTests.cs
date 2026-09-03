using AdrGuard.Parsing;
using Xunit;

namespace AdrGuard.Tests.Parsing;

public sealed class AdrMarkdownParserTests
{
    [Fact]
    public void ParseExtractsDocumentMetadataAndSections()
    {
        const string markdown = """
            # Use PostgreSQL

            ## Status

            Accepted

            ## Context

            We need a relational database.

            ## Decision

            PostgreSQL will be used.
            """;

        var document = AdrMarkdownParser.Parse("docs/adr/0007-use-postgresql.md", markdown);

        Assert.Equal("0007-use-postgresql.md", document.FileName);
        Assert.Equal(7, document.Id);
        Assert.Equal("use-postgresql", document.Slug);
        Assert.Equal("Use PostgreSQL", document.Title);
        Assert.Equal("Accepted", document.Status);
        Assert.Collection(
            document.Sections,
            section =>
            {
                Assert.Equal("Status", section.Heading);
                Assert.Equal(2, section.Level);
                Assert.Equal("Accepted", section.Content);
            },
            section =>
            {
                Assert.Equal("Context", section.Heading);
                Assert.Equal("We need a relational database.", section.Content);
            },
            section =>
            {
                Assert.Equal("Decision", section.Heading);
                Assert.Equal("PostgreSQL will be used.", section.Content);
            });
    }

    [Fact]
    public void ParseLeavesFilenameMetadataNullableWhenPrefixCannotBeParsed()
    {
        const string markdown = "# Untitled decision";

        var document = AdrMarkdownParser.Parse("docs/adr/example.md", markdown);

        Assert.Null(document.Id);
        Assert.Null(document.Slug);
        Assert.Equal("Untitled decision", document.Title);
    }

    [Fact]
    public void ParseTreatsStatusHeadingCaseInsensitively()
    {
        const string markdown = """
            # Decision

            ## status
            Proposed
            """;

        var document = AdrMarkdownParser.Parse("0001-decision.md", markdown);

        Assert.Equal("Proposed", document.Status);
    }

    [Fact]
    public void ParseIgnoresHeadingsInsideFencedCodeBlocks()
    {
        const string markdown = """
            # Decision

            ## Context

            ```text
            ## Not a section
            # Not a title
            ```

            Still context.

            ## Decision

            Use the real section.
            """;

        var document = AdrMarkdownParser.Parse("0001-decision.md", markdown);

        Assert.Equal("Decision", document.Title);
        Assert.Equal(2, document.Sections.Count);
        Assert.DoesNotContain(document.Sections, section => section.Heading == "Not a section");
        Assert.Contains("## Not a section", document.Sections[0].Content, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseSupportsTildeFencedCodeBlocks()
    {
        const string markdown = """
            # Decision

            ## Context

            ~~~markdown
            ## Not a section
            ~~~
            """;

        var document = AdrMarkdownParser.Parse("0001-decision.md", markdown);

        Assert.Single(document.Sections);
        Assert.Equal("Context", document.Sections[0].Heading);
    }
}
