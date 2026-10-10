using AdrGuard.Indexing;
using AdrGuard.Parsing;
using Xunit;

namespace AdrGuard.Tests.Indexing;

public sealed class AdrIndexGeneratorTests
{
    [Fact]
    public void EnrichedCatalogUsesProvidedMetadataAndExplicitUnknownsDeterministically()
    {
        var withMetadata = AdrMarkdownParser.Parse("0001-first.md", """
            ---
            date: 2026-10-09
            owner: Platform | Team
            category: infrastructure
            last-reviewed: 2026-10-10
            ---
            # First
            ## Status
            Accepted
            ## Context
            Context.
            ## Decision
            Decision.
            ## Consequences
            Consequences.
            """);
        var withoutMetadata = AdrMarkdownParser.Parse("0002-second.md", ValidMarkdown("Second", "Proposed"));

        var first = AdrIndexGenerator.GenerateEnriched([withoutMetadata, withMetadata]);
        var second = AdrIndexGenerator.GenerateEnriched([withMetadata, withoutMetadata]);

        Assert.Equal(first, second);
        Assert.Contains("Platform \\| Team", first, StringComparison.Ordinal);
        Assert.Contains("2026-10-09", first, StringComparison.Ordinal);
        Assert.Contains("unknown", first, StringComparison.Ordinal);
    }

    [Fact]
    public void EnrichedCatalogScalesDeterministicallyToOneThousandAdrs()
    {
        var documents = Enumerable.Range(1, 1000)
            .Select(id => AdrMarkdownParser.Parse($"{id:D4}-decision.md", ValidMarkdown($"Decision {id}", "Accepted")))
            .ToArray();
        Assert.Equal(AdrIndexGenerator.GenerateEnriched(documents), AdrIndexGenerator.GenerateEnriched(documents.Reverse().ToArray()));
    }

    [Fact]
    public void GenerateEscapesTableCellsAndSortsById()
    {
        var documents = new[]
        {
            AdrMarkdownParser.Parse(
                "0002-second.md",
                ValidMarkdown("Second | Decision", "Proposed")),
            AdrMarkdownParser.Parse(
                "0001-first.md",
                ValidMarkdown("First", "Accepted")),
        };

        var index = AdrIndexGenerator.Generate(documents);

        var firstPosition = index.IndexOf("[0001]", StringComparison.Ordinal);
        var secondPosition = index.IndexOf("[0002]", StringComparison.Ordinal);

        Assert.True(firstPosition >= 0);
        Assert.True(secondPosition > firstPosition);
        Assert.Contains("Second \\| Decision", index, StringComparison.Ordinal);
    }

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
