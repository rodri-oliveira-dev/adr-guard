using AdrGuard.Exporting;
using AdrGuard.Model;
using AdrGuard.Parsing;
using AdrGuard.Validation;
using Xunit;

namespace AdrGuard.Tests.Exporting;

public sealed class AdrRelationshipsExportBuilderTests
{
    [Fact]
    public void CreatePreservesDeclarationsAndSeparatesResolutionFromSemanticValidity()
    {
        using var repository = new TemporaryRepository();
        var first = Parse(
            repository,
            "docs/adr/0001-first.md",
            Canonical(
                "First",
                "Accepted",
                """
                ## Superseded by
                [Self](0001-first.md), [Second](0002-second.md), and [Missing](0099-missing.md).
                """));
        var second = Parse(
            repository,
            "docs/adr/0002-second.md",
            Canonical(
                "Second",
                "Superseded",
                """
                ## Superseded by
                [First](0001-first.md)
                ## Supersedes
                [First](0001-first.md)
                ## Depends on
                [Deprecated](0003-deprecated.md)
                """));
        var deprecated = Parse(
            repository,
            "docs/adr/0003-deprecated.md",
            Canonical("Deprecated", "Deprecated"));
        var invalidStatus = Parse(
            repository,
            "docs/adr/0004-invalid.md",
            Canonical("Invalid", "Approved"));

        var export = AdrRelationshipsExportBuilder.Create(
            [invalidStatus, deprecated, second, first],
            repository.Root,
            new AdrValidationOptions(AdrFormat.Canonical));

        Assert.Equal(["ADR-1", "ADR-2", "ADR-3", "ADR-4"], export.Adrs.Select(adr => adr.Identifier));
        Assert.Equal(
            [
                AdrRelationshipExportStatus.Accepted,
                AdrRelationshipExportStatus.Superseded,
                AdrRelationshipExportStatus.Deprecated,
                null,
            ],
            export.Adrs.Select(adr => adr.Status));
        Assert.All(export.Adrs, adr => AssertPortablePath(adr.Path));

        Assert.Equal(6, export.Relationships.Count);
        Assert.Contains(export.Relationships, relationship =>
            relationship.SourceIdentifier == "ADR-1"
            && relationship.DeclaredTarget == "0001-first.md"
            && relationship.Resolution == AdrRelationshipExportResolution.Resolved
            && relationship.TargetIdentifier == "ADR-1");
        Assert.Contains(export.Relationships, relationship =>
            relationship.SourceIdentifier == "ADR-1"
            && relationship.DeclaredTarget == "0099-missing.md"
            && relationship.Resolution == AdrRelationshipExportResolution.Unresolved
            && relationship.TargetIdentifier is null
            && relationship.TargetPath is null);
        Assert.Equal(
            [AdrRelationshipExportType.SupersededBy, AdrRelationshipExportType.Supersedes],
            export.Relationships
                .Where(relationship => relationship.SourceIdentifier == "ADR-2"
                    && relationship.TargetIdentifier == "ADR-1")
                .Select(relationship => relationship.Type));

        Assert.Contains(export.Issues, issue => issue.Code == ValidationCodes.BrokenReference);
        Assert.Contains(export.Issues, issue => issue.Code == ValidationCodes.SupersessionCycle);
        Assert.Contains(export.Issues, issue => issue.Code == ValidationCodes.SelfSupersession);
        Assert.Contains(export.Issues, issue => issue.Code == ValidationCodes.MultipleSuperseders);
        Assert.Contains(export.Issues, issue => issue.Code == ValidationCodes.InconsistentSupersession);
        Assert.Contains(export.Issues, issue => issue.Code == ValidationCodes.InactiveDependency);
        Assert.Contains(export.Issues, issue => issue.Code == ValidationCodes.InvalidStatus);
        Assert.All(export.Issues, issue => AssertPortablePath(issue.Path));
    }

    [Fact]
    public void CreateMarksDuplicateMadrTargetsAmbiguousWithoutChoosingACandidate()
    {
        using var repository = new TemporaryRepository();
        var source = Parse(
            repository,
            "docs/adr/0001-source.md",
            Madr("Source", "superseded by ADR-0002"));
        var firstCandidate = Parse(
            repository,
            "docs/adr/0002-first.md",
            Madr("First candidate", "accepted"));
        var secondCandidate = Parse(
            repository,
            "docs/adr/nested/0002-second.md",
            Madr("Second candidate", "accepted"));

        var export = AdrRelationshipsExportBuilder.Create(
            [source, firstCandidate, secondCandidate],
            repository.Root,
            new AdrValidationOptions(AdrFormat.Madr4));

        var relationship = Assert.Single(export.Relationships);
        Assert.Equal(AdrRelationshipExportResolution.Ambiguous, relationship.Resolution);
        Assert.Null(relationship.TargetIdentifier);
        Assert.Null(relationship.TargetPath);
        Assert.Contains(export.Issues, issue => issue.Code == ValidationCodes.DuplicateId);
        Assert.Contains(export.Issues, issue =>
            issue.Code == ValidationCodes.BrokenReference
            && issue.Message.Contains("does not resolve to a unique ADR", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateIsDeterministicallyOrderedForEquivalentInputSets()
    {
        using var repository = new TemporaryRepository();
        var first = Parse(
            repository,
            "docs/adr/0001-first.md",
            Canonical("First", "Accepted", "## Depends on\n[Third](0003-third.md)"));
        var second = Parse(
            repository,
            "docs/adr/0002-second.md",
            Canonical("Second", "Accepted", "## Depends on\n[First](0001-first.md)"));
        var third = Parse(repository, "docs/adr/0003-third.md", Canonical("Third", "Accepted"));
        var options = new AdrValidationOptions(AdrFormat.Canonical);

        var forward = AdrRelationshipsExportBuilder.Create([first, second, third], repository.Root, options);
        var reverse = AdrRelationshipsExportBuilder.Create([third, second, first], repository.Root, options);

        Assert.Equal(forward.Adrs, reverse.Adrs);
        Assert.Equal(forward.Relationships, reverse.Relationships);
        Assert.Equal(forward.Issues, reverse.Issues);
        Assert.Equal(
            ["ADR-1", "ADR-2"],
            forward.Relationships.Select(relationship => relationship.SourceIdentifier));
    }

    [Fact]
    public void CreateRejectsDocumentPathsOutsideRepositoryRoot()
    {
        using var repository = new TemporaryRepository();
        var outside = AdrMarkdownParser.Parse(
            Path.Combine(Path.GetTempPath(), $"adr-guard-outside-{Guid.NewGuid():N}.md"),
            Canonical("Outside", "Accepted"));

        Assert.Throws<ArgumentException>(() => AdrRelationshipsExportBuilder.Create(
            [outside],
            repository.Root,
            new AdrValidationOptions(AdrFormat.Canonical)));
    }

    private static AdrDocument Parse(TemporaryRepository repository, string relativePath, string markdown) =>
        AdrMarkdownParser.Parse(Path.Combine(repository.Root, relativePath.Replace('/', Path.DirectorySeparatorChar)), markdown);

    private static void AssertPortablePath(string path)
    {
        Assert.False(Path.IsPathRooted(path));
        Assert.DoesNotContain('\\', path);
        Assert.DoesNotContain("../", path, StringComparison.Ordinal);
        Assert.StartsWith("docs/adr/", path, StringComparison.Ordinal);
    }

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
        Chosen option: "Option".
        """;

    private sealed class TemporaryRepository : IDisposable
    {
        internal TemporaryRepository()
        {
            Root = Path.Combine(Path.GetTempPath(), $"adr-guard-export-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        internal string Root { get; }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
