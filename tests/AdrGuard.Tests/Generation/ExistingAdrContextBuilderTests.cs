using AdrGuard.Generation;
using AdrGuard.Model;
using Xunit;

namespace AdrGuard.Tests.Generation;

public sealed class ExistingAdrContextBuilderTests
{
    [Fact]
    public void BuildOrdersAdrsByIdAndPreservesDecisionMetadataAndRelationships()
    {
        var root = Path.Combine(Path.GetTempPath(), "adr-guard-context-tests");

        var second = CreateDocument(
            root,
            2,
            "0002-use-redis.md",
            "Use Redis",
            "Proposed",
            "Use Redis for distributed caching.",
            "[Related decision](0001-use-postgresql.md)");

        var first = CreateDocument(
            root,
            1,
            "0001-use-postgresql.md",
            "Use PostgreSQL",
            "Accepted",
            "Use PostgreSQL as the relational database.",
            null);

        var result = ExistingAdrContextBuilder.Build([second, first]);

        Assert.Equal(2, result.IncludedCount);
        Assert.Equal(2, result.TotalCount);
        Assert.False(result.IsBounded);

        var firstIndex = result.Content.IndexOf("ADR 0001", StringComparison.Ordinal);
        var secondIndex = result.Content.IndexOf("ADR 0002", StringComparison.Ordinal);

        Assert.True(firstIndex >= 0);
        Assert.True(secondIndex > firstIndex);
        Assert.Contains("Title: Use PostgreSQL", result.Content, StringComparison.Ordinal);
        Assert.Contains("Status: Accepted", result.Content, StringComparison.Ordinal);
        Assert.Contains(
            "Use PostgreSQL as the relational database.",
            result.Content,
            StringComparison.Ordinal);
        Assert.Contains(
            "- 0001-use-postgresql.md",
            result.Content,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BuildUsesDeterministicFilenameOrderingWhenIdsAreEqual()
    {
        var root = Path.Combine(Path.GetTempPath(), "adr-guard-context-tests");

        var beta = CreateDocument(
            root,
            1,
            "0001-beta.md",
            "Beta",
            "Accepted",
            "Beta decision.",
            null);

        var alpha = CreateDocument(
            root,
            1,
            "0001-alpha.md",
            "Alpha",
            "Accepted",
            "Alpha decision.",
            null);

        var result = ExistingAdrContextBuilder.Build([beta, alpha]);

        var alphaIndex = result.Content.IndexOf("Title: Alpha", StringComparison.Ordinal);
        var betaIndex = result.Content.IndexOf("Title: Beta", StringComparison.Ordinal);

        Assert.True(alphaIndex >= 0);
        Assert.True(betaIndex > alphaIndex);
    }

    [Fact]
    public void BuildStopsBeforeAnAdrThatWouldExceedTheCharacterLimit()
    {
        var root = Path.Combine(Path.GetTempPath(), "adr-guard-context-tests");
        var documents = Enumerable
            .Range(1, 20)
            .Select(id => CreateDocument(
                root,
                id,
                $"{id:D4}-decision-{id:D4}.md",
                $"Decision {id:D4}",
                "Accepted",
                new string((char)('a' + (id % 26)), 900),
                null))
            .ToArray();

        var first = ExistingAdrContextBuilder.Build(documents);
        var second = ExistingAdrContextBuilder.Build(documents.Reverse().ToArray());

        Assert.True(first.IsBounded);
        Assert.True(first.IncludedCount < first.TotalCount);
        Assert.InRange(first.Content.Length, 1, ExistingAdrContextBuilder.MaximumContextCharacters);
        Assert.Equal(first.Content, second.Content);
        Assert.Equal(first.IncludedCount, second.IncludedCount);

        var firstExcludedId = first.IncludedCount + 1;
        Assert.DoesNotContain(
            $"ADR {firstExcludedId:D4}",
            first.Content,
            StringComparison.Ordinal);
    }

    private static AdrDocument CreateDocument(
        string root,
        int id,
        string fileName,
        string title,
        string status,
        string decision,
        string? relationship)
    {
        var sections = new List<AdrSection>
        {
            new("Status", 2, status),
            new("Context", 2, "Context that must not be copied into the compact ADR representation."),
            new("Decision", 2, decision),
            new("Consequences", 2, "Consequences."),
        };

        if (!string.IsNullOrWhiteSpace(relationship))
        {
            sections.Add(new AdrSection("Related", 2, relationship));
        }

        return new AdrDocument(
            Path.Combine(root, fileName),
            fileName,
            id,
            Path.GetFileNameWithoutExtension(fileName)[5..],
            title,
            status,
            sections);
    }
}
