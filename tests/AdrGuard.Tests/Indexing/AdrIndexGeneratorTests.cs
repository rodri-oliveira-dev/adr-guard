using AdrGuard.Indexing;
using AdrGuard.Parsing;
using Xunit;

namespace AdrGuard.Tests.Indexing;

public sealed class AdrIndexGeneratorTests
{
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
