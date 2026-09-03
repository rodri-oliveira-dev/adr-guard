using AdrGuard.Model;
using AdrGuard.Parsing;
using AdrGuard.Validation;
using Xunit;

namespace AdrGuard.Tests.Validation;

public sealed class AdrValidatorTests
{
    [Fact]
    public void ValidateAcceptsWellFormedAdrSet()
    {
        var documents = new[]
        {
            Parse("docs/adr/0001-use-postgresql.md", ValidMarkdown("Use PostgreSQL", "Accepted")),
            Parse("docs/adr/0002-use-redis.md", ValidMarkdown("Use Redis", "Proposed")),
        };

        var result = AdrValidator.Validate(documents);

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Theory]
    [InlineData("1-use-postgresql.md")]
    [InlineData("0000-use-postgresql.md")]
    [InlineData("0001-Use-PostgreSQL.md")]
    [InlineData("0001-use--postgresql.md")]
    [InlineData("0001-use-postgresql.MD")]
    public void ValidateReportsInvalidFileName(string fileName)
    {
        var document = Parse(fileName, ValidMarkdown("Use PostgreSQL", "Accepted"));

        var result = AdrValidator.Validate([document]);

        Assert.Contains(result.Issues, issue => issue.Code == ValidationCodes.InvalidFileName);
    }

    [Fact]
    public void ValidateReportsMissingTitle()
    {
        const string markdown = """
            ## Status
            Accepted

            ## Context
            Context.

            ## Decision
            Decision.

            ## Consequences
            Consequences.
            """;

        var result = AdrValidator.Validate([Parse("0001-no-title.md", markdown)]);

        Assert.Contains(result.Issues, issue => issue.Code == ValidationCodes.MissingTitle);
    }

    [Fact]
    public void ValidateReportsMissingStatus()
    {
        const string markdown = """
            # Decision

            ## Context
            Context.

            ## Decision
            Decision.

            ## Consequences
            Consequences.
            """;

        var result = AdrValidator.Validate([Parse("0001-decision.md", markdown)]);

        Assert.Contains(result.Issues, issue => issue.Code == ValidationCodes.MissingStatus);
    }

    [Fact]
    public void ValidateReportsInvalidStatus()
    {
        var document = Parse("0001-decision.md", ValidMarkdown("Decision", "Approved"));

        var result = AdrValidator.Validate([document]);

        var issue = Assert.Single(result.Issues, issue => issue.Code == ValidationCodes.InvalidStatus);
        Assert.Contains("Proposed", issue.Message, StringComparison.Ordinal);
        Assert.Contains("Superseded", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateReportsMissingOrEmptyRequiredSections()
    {
        const string markdown = """
            # Decision

            ## Status
            Accepted

            ## Context

            ## Decision
            Decision.
            """;

        var result = AdrValidator.Validate([Parse("0001-decision.md", markdown)]);

        var missingSections = result.Issues
            .Where(issue => issue.Code == ValidationCodes.MissingSection)
            .ToArray();

        Assert.Equal(2, missingSections.Length);
        Assert.Contains(missingSections, issue => issue.Message.Contains("Context", StringComparison.Ordinal));
        Assert.Contains(missingSections, issue => issue.Message.Contains("Consequences", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateReportsDuplicateIdsForEveryConflictingAdr()
    {
        var documents = new[]
        {
            Parse("docs/adr/0001-first.md", ValidMarkdown("First", "Accepted")),
            Parse("docs/other/0001-second.md", ValidMarkdown("Second", "Accepted")),
        };

        var result = AdrValidator.Validate(documents);

        var duplicateIssues = result.Issues
            .Where(issue => issue.Code == ValidationCodes.DuplicateId)
            .ToArray();

        Assert.Equal(2, duplicateIssues.Length);
        Assert.All(duplicateIssues, issue => Assert.Contains("0001", issue.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateAcceptsRelativeReferencesToKnownAdrs()
    {
        const string firstMarkdown = """
            # First

            ## Status
            Accepted

            ## Context
            See [the next ADR](0002-second.md).

            ## Decision
            Decision.

            ## Consequences
            Consequences.
            """;

        var documents = new[]
        {
            Parse("docs/adr/0001-first.md", firstMarkdown),
            Parse("docs/adr/0002-second.md", ValidMarkdown("Second", "Accepted")),
        };

        var result = AdrValidator.Validate(documents);

        Assert.DoesNotContain(result.Issues, issue => issue.Code == ValidationCodes.BrokenReference);
    }

    [Fact]
    public void ValidateReportsBrokenLocalMarkdownReference()
    {
        const string markdown = """
            # First

            ## Status
            Accepted

            ## Context
            See [missing ADR](0009-missing.md).

            ## Decision
            Decision.

            ## Consequences
            Consequences.
            """;

        var result = AdrValidator.Validate([Parse("docs/adr/0001-first.md", markdown)]);

        var issue = Assert.Single(result.Issues, issue => issue.Code == ValidationCodes.BrokenReference);
        Assert.Contains("0009-missing.md", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateIgnoresExternalAndAnchorLinks()
    {
        const string markdown = """
            # First

            ## Status
            Accepted

            ## Context
            See [docs](https://example.com/reference.md) and [Decision](#decision).

            ## Decision
            Decision.

            ## Consequences
            Consequences.
            """;

        var result = AdrValidator.Validate([Parse("docs/adr/0001-first.md", markdown)]);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateRequiresSupersededByLinkForSupersededAdr()
    {
        var document = Parse("docs/adr/0001-first.md", ValidMarkdown("First", "Superseded"));

        var result = AdrValidator.Validate([document]);

        Assert.Contains(result.Issues, issue => issue.Code == ValidationCodes.MissingSupersededBy);
    }

    [Fact]
    public void ValidateAcceptsSupersededByLinkToExistingAdr()
    {
        const string firstMarkdown = """
            # First

            ## Status
            Superseded

            ## Context
            Context.

            ## Decision
            Decision.

            ## Consequences
            Consequences.

            ## Superseded by
            [ADR 0002](0002-second.md)
            """;

        var documents = new[]
        {
            Parse("docs/adr/0001-first.md", firstMarkdown),
            Parse("docs/adr/0002-second.md", ValidMarkdown("Second", "Accepted")),
        };

        var result = AdrValidator.Validate(documents);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateReturnsIssuesInDeterministicOrder()
    {
        var documents = new[]
        {
            Parse("docs/adr/0002-second.md", "# Second"),
            Parse("docs/adr/0001-first.md", "# First"),
        };

        var result = AdrValidator.Validate(documents);

        Assert.Equal(
            result.Issues
                .OrderBy(issue => issue.FilePath, StringComparer.Ordinal)
                .ThenBy(issue => issue.Code, StringComparer.Ordinal)
                .ThenBy(issue => issue.Message, StringComparer.Ordinal),
            result.Issues);
    }

    private static AdrDocument Parse(string filePath, string markdown) =>
        AdrMarkdownParser.Parse(filePath, markdown);

    private static string ValidMarkdown(string title, string status) =>
        $"""
        # {title}

        ## Status
        {status}

        ## Context
        Context.

        ## Decision
        Decision.

        ## Consequences
        Consequences.
        """;
}
