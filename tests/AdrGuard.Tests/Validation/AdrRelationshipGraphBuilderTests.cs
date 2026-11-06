using AdrGuard.Parsing;
using AdrGuard.Validation;
using Xunit;

namespace AdrGuard.Tests.Validation;

public sealed class AdrRelationshipGraphBuilderTests
{
    [Fact]
    public void BuildReturnsOnlyExplicitCanonicalRelationshipTypes()
    {
        var root = Path.Combine(Path.GetTempPath(), $"adr-graph-core-{Guid.NewGuid():N}");
        var first = Parse(root, "0001-first.md", Canonical(
            "First",
            "Accepted",
            "## Depends on\n\n- [Second](0002-second.md)\n"));
        var second = Parse(root, "0002-second.md", Canonical(
            "Second",
            "Superseded",
            "## Superseded by\n\n- [Third](0003-third.md)\n"));
        var third = Parse(root, "0003-third.md", Canonical(
            "Third",
            "Accepted",
            "## Supersedes\n\n- [Second](0002-second.md)\n"));
        var options = new AdrValidationOptions(AdrFormat.Canonical, root);

        var graph = AdrRelationshipGraphBuilder.Build([first, second, third], options);

        Assert.Equal(3, graph.Nodes.Count);
        Assert.Equal(
            ["depends-on", "superseded-by", "supersedes"],
            graph.Relationships.Select(edge => edge.Type).Order().ToArray());
        Assert.All(graph.Relationships, edge => Assert.Equal(AdrRelationshipResolution.Resolved, edge.Resolution));
    }

    [Fact]
    public void BuildReportsAmbiguousMadrIdentifierWithoutInferringATarget()
    {
        var root = Path.Combine(Path.GetTempPath(), $"adr-graph-core-{Guid.NewGuid():N}");
        var source = Parse(root, "0001-source.md", Madr("Source", "superseded by ADR-0002"));
        var firstTarget = Parse(root, "0002-first.md", Madr("First target", "accepted"));
        var secondTarget = Parse(root, "0002-second.md", Madr("Second target", "accepted"));
        var options = new AdrValidationOptions(AdrFormat.Madr4, root);

        var relationship = Assert.Single(
            AdrRelationshipGraphBuilder.Build([source, firstTarget, secondTarget], options).Relationships);

        Assert.Equal("superseded-by", relationship.Type);
        Assert.Equal("ADR-2", relationship.DeclaredTarget);
        Assert.Equal(AdrRelationshipResolution.Ambiguous, relationship.Resolution);
        Assert.Equal(2, relationship.Targets.Count);
    }

    private static AdrGuard.Model.AdrDocument Parse(string root, string fileName, string markdown) =>
        AdrMarkdownParser.Parse(Path.Combine(root, fileName), markdown);

    private static string Canonical(string title, string status, string relationships) => $$"""
        # {{title}}

        ## Status
        {{status}}

        ## Context
        Context.

        ## Decision
        Decision.

        ## Consequences
        Consequences.

        {{relationships}}
        """;

    private static string Madr(string title, string status) => $$"""
        ---
        status: {{status}}
        ---

        # {{title}}

        ## Context and Problem Statement
        Context.

        ## Considered Options
        Options.

        ## Decision Outcome
        Outcome.
        """;
}
