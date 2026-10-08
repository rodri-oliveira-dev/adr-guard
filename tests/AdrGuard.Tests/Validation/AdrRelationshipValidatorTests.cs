using AdrGuard.Model;
using AdrGuard.Parsing;
using AdrGuard.Validation;
using Xunit;

namespace AdrGuard.Tests.Validation;

public sealed class AdrRelationshipValidatorTests
{
    [Fact]
    public void ValidSupersessionPairIsAccepted()
    {
        var first = Parse(
            "0001-first.md",
            Canonical("First", "Superseded", "## Superseded by\n[ADR 0002](0002-second.md)"));
        var second = Parse(
            "0002-second.md",
            Canonical("Second", "Accepted", "## Supersedes\n[ADR 0001](0001-first.md)"));

        var result = AdrValidator.Validate([first, second]);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void SupersessionCycleIsReportedForEachParticipantOnly()
    {
        var documents = new[]
        {
            Parse("0001-first.md", Canonical("First", "Superseded", "## Superseded by\n[Second](0002-second.md)")),
            Parse("0002-second.md", Canonical("Second", "Superseded", "## Superseded by\n[Third](0003-third.md)")),
            Parse("0003-third.md", Canonical("Third", "Superseded", "## Superseded by\n[First](0001-first.md)")),
            Parse("0004-unrelated.md", Canonical("Unrelated", "Accepted")),
        };

        var issues = AdrValidator.Validate(documents).Issues
            .Where(issue => issue.Code == ValidationCodes.SupersessionCycle)
            .ToArray();

        Assert.Equal(3, issues.Length);
        Assert.DoesNotContain(issues, issue => issue.FilePath.EndsWith("0004-unrelated.md", StringComparison.Ordinal));
    }

    [Fact]
    public void SelfAndMultipleSupersessionAreObjectiveDiagnostics()
    {
        var documents = new[]
        {
            Parse(
                "0001-first.md",
                Canonical(
                    "First",
                    "Superseded",
                    "## Superseded by\n[Self](0001-first.md) and [Second](0002-second.md)")),
            Parse("0002-second.md", Canonical("Second", "Accepted")),
        };

        var result = AdrValidator.Validate(documents);

        Assert.Contains(result.Issues, issue => issue.Code == ValidationCodes.SelfSupersession);
        Assert.Contains(result.Issues, issue => issue.Code == ValidationCodes.MultipleSuperseders);
    }

    [Fact]
    public void ExplicitDependencyOnInactiveAdrIsReportedButNarrativeLinkIsNot()
    {
        var inactive = Parse("0001-old.md", Canonical("Old", "Deprecated"));
        var dependency = Parse(
            "0002-dependent.md",
            Canonical("Dependent", "Accepted", "## Dependencies\n[Old](0001-old.md)"));
        var narrative = Parse(
            "0003-narrative.md",
            Canonical("Narrative", "Accepted", "## References\n[Old](0001-old.md)"));

        var result = AdrValidator.Validate([inactive, dependency, narrative]);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == ValidationCodes.InactiveDependency
                && issue.FilePath.EndsWith("0002-dependent.md", StringComparison.Ordinal));
        Assert.DoesNotContain(
            result.Issues,
            issue => issue.Code == ValidationCodes.InactiveDependency
                && issue.FilePath.EndsWith("0003-narrative.md", StringComparison.Ordinal));
    }

    [Fact]
    public void CanonicalStatusSectionCannotBeOverriddenByFrontMatter()
    {
        var accepted = Parse(
            "0001-current.md",
            "---\nstatus: Superseded\n---\n" + Canonical("Current", "Accepted"));
        var superseded = Parse(
            "0002-old.md",
            "---\nstatus: Accepted\n---\n" + Canonical("Old", "Superseded"));

        Assert.Equal("Accepted", accepted.Status);
        Assert.Equal("Superseded", superseded.Status);

        var result = AdrValidator.Validate([accepted, superseded]);

        Assert.Contains(result.Issues, issue =>
            issue.FilePath.EndsWith("0002-old.md", StringComparison.Ordinal)
            && issue.Code == ValidationCodes.MissingSupersededBy);
        Assert.DoesNotContain(result.Issues, issue =>
            issue.FilePath.EndsWith("0001-current.md", StringComparison.Ordinal)
            && issue.Code == ValidationCodes.MissingSupersededBy);
    }

    [Fact]
    public void NestedRelationshipHeadingsAreNotGraphDeclarations()
    {
        var old = Parse("0001-old.md", Canonical("Old", "Deprecated"));
        var narrative = Parse(
            "0002-narrative.md",
            Canonical("Narrative", "Accepted",
                "## References\n### Dependencies\n[Old](0001-old.md)\n### Superseded by\n[Old](0001-old.md)\n### Supersedes\n[Old](0001-old.md)"));

        var result = AdrValidator.Validate([old, narrative]);

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Issues, issue =>
            issue.Code is ValidationCodes.InactiveDependency
                or ValidationCodes.InconsistentSupersession
                or ValidationCodes.SupersessionCycle);
    }

    [Fact]
    public void MadrSupersededByStatusResolvesByUniqueId()
    {
        var old = Parse(
            "0001-old.md",
            Madr("Old", "superseded by ADR-0002"));
        var current = Parse("0002-current.md", Madr("Current", "accepted"));

        var result = AdrValidator.Validate([old, current], AdrFormat.Madr4);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void LargeAcyclicGraphIsDeterministic()
    {
        const int count = 1000;
        var documents = new List<AdrDocument>(count);
        for (var id = 1; id <= count; id++)
        {
            var relationship = id == count
                ? string.Empty
                : $"## Superseded by\n[Next]({id + 1:D4}-decision.md)";
            var status = id == count ? "Accepted" : "Superseded";
            documents.Add(Parse($"{id:D4}-decision.md", Canonical($"Decision {id}", status, relationship)));
        }

        var first = AdrValidator.Validate(documents);
        var second = AdrValidator.Validate(documents.AsEnumerable().Reverse().ToArray());

        Assert.DoesNotContain(first.Issues, issue => issue.Code == ValidationCodes.SupersessionCycle);
        Assert.Equal(first.Issues, second.Issues);
    }

    private static AdrDocument Parse(string path, string markdown) =>
        AdrMarkdownParser.Parse(Path.Combine("docs", "adr", path), markdown);

    private static string Canonical(string title, string status, string additional = "") =>
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
        {additional}
        """;

    private static string Madr(string title, string status) =>
        $"""
        ---
        status: "{status}"
        ---
        # {title}
        ## Context and Problem Statement
        Context.
        ## Considered Options
        * Option
        ## Decision Outcome
        Chosen option: "Option", because it meets the requirements.
        """;
}
