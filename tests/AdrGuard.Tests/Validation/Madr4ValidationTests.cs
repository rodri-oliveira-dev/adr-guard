using AdrGuard.Parsing;
using AdrGuard.Validation;
using Xunit;

namespace AdrGuard.Tests.Validation;

public sealed class Madr4ValidationTests
{
    [Theory]
    [InlineData("minimal.md")]
    [InlineData("full.md")]
    public void PinnedOfficialStructureFixtureIsConformant(string fileName)
    {
        var markdown = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Madr4", fileName));
        var document = AdrMarkdownParser.Parse($"0001-{Path.GetFileNameWithoutExtension(fileName)}.md", markdown);

        var result = AdrValidator.Validate([document], AdrFormat.Madr4);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Madr4AcceptsOfficialMinimalStructureWhenExplicitlySelected()
    {
        const string markdown = """
            # Use PostgreSQL for transactional data

            ## Context and Problem Statement

            The service requires transactional persistence.

            ## Considered Options

            * PostgreSQL
            * SQLite

            ## Decision Outcome

            Chosen option: "PostgreSQL", because it meets the concurrency and durability requirements.

            ### Consequences

            * Good, because transactions are supported.
            * Bad, because operations require a managed database.
            """;

        var document = AdrMarkdownParser.Parse("0001-use-postgresql.md", markdown);
        var result = AdrValidator.Validate([document], AdrFormat.Madr4);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Madr4ReadsOptionalFrontMatterStatusAndAcceptsFullOptionalSections()
    {
        const string markdown = """
            ---
            status: "accepted"
            date: 2026-10-08
            decision-makers: Architecture team
            ---

            # Use PostgreSQL

            ## Context and Problem Statement
            We need durable storage.

            ## Decision Drivers
            * Durability

            ## Considered Options
            * PostgreSQL
            * SQLite

            ## Decision Outcome
            Chosen option: "PostgreSQL", because it satisfies the drivers.

            ### Confirmation
            An integration test confirms transaction behavior.

            ## Pros and Cons of the Options
            ### PostgreSQL
            * Good, because it supports the required isolation.

            ## More Information
            See the database runbook.
            """;

        var document = AdrMarkdownParser.Parse("0001-use-postgresql.md", markdown);
        var result = AdrValidator.Validate([document], AdrFormat.Madr4);

        Assert.Equal("accepted", document.Status);
        Assert.Equal("2026-10-08", document.Metadata!["date"]);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Madr4MetadataStatusWinsOverSupplementalStatusSection()
    {
        const string markdown = """
            ---
            status: accepted
            ---
            # Use PostgreSQL
            ## Status
            Superseded
            ## Context and Problem Statement
            We need durable storage.
            ## Considered Options
            * PostgreSQL
            ## Decision Outcome
            Chosen option: PostgreSQL for durability.
            """;

        var document = AdrMarkdownParser.Parse("0001-use-postgresql.md", markdown);
        var result = AdrValidator.Validate([document], AdrFormat.Madr4);

        Assert.True(result.IsValid);
        Assert.Equal("accepted", Assert.Single(AdrStatusResolver.ForFormat([document], AdrFormat.Madr4)).Status);
    }

    [Theory]
    [InlineData("Context and Problem Statement")]
    [InlineData("Considered Options")]
    [InlineData("Decision Outcome")]
    public void Madr4RequiresEachMandatorySection(string omittedSection)
    {
        var sections = new Dictionary<string, string>
        {
            ["Context and Problem Statement"] = "Context.",
            ["Considered Options"] = "* Option A",
            ["Decision Outcome"] = "Chosen option: A, because it meets the requirements.",
        };
        sections.Remove(omittedSection);
        var markdown = "# Decision\n\n" + string.Join(
            "\n\n",
            sections.Select(item => $"## {item.Key}\n{item.Value}"));

        var result = AdrValidator.Validate(
            [AdrMarkdownParser.Parse("0001-decision.md", markdown)],
            AdrFormat.Madr4);

        var issue = Assert.Single(result.Issues, item => item.Code == ValidationCodes.MissingSection);
        Assert.Contains(omittedSection, issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatIsNeverAutoDetected()
    {
        const string canonical = """
            # Canonical
            ## Status
            Accepted
            ## Context
            Context.
            ## Decision
            Decision.
            ## Consequences
            Consequences.
            """;
        var document = AdrMarkdownParser.Parse("0001-canonical.md", canonical);

        Assert.True(AdrValidator.Validate([document]).IsValid);
        Assert.False(AdrValidator.Validate([document], AdrFormat.Madr4).IsValid);
    }
}
