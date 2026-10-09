using AdrGuard.Parsing;
using AdrGuard.Validation;
using Xunit;

namespace AdrGuard.Tests.Validation;

public sealed class AdrReferenceCompatibilityTests
{
    [Fact]
    public void ExistingRepositoryDocumentIsNotRequiredToBeAnAdr()
    {
        using var repository = TemporaryRepository.Create();
        var adrPath = repository.Write("docs/adr/0001-decision.md", Canonical("## References\n[Architecture](../architecture.md#overview \"guide\")"));
        repository.Write("docs/architecture.md", "# Architecture");
        var document = AdrMarkdownParser.Parse(adrPath, File.ReadAllText(adrPath));
        var result = AdrValidator.Validate([document], null, AdrFormat.Canonical, repository.Root);
        Assert.DoesNotContain(result.Issues, issue => issue.Code == ValidationCodes.BrokenReference);
    }

    [Fact]
    public void RelationshipTargetMustBelongToValidatedAdrSet()
    {
        using var repository = TemporaryRepository.Create();
        var adrPath = repository.Write("docs/adr/0001-decision.md", Canonical("## Dependencies\n[Document](../architecture.md)"));
        repository.Write("docs/architecture.md", "# Architecture");
        var document = AdrMarkdownParser.Parse(adrPath, File.ReadAllText(adrPath));
        var result = AdrValidator.Validate([document], null, AdrFormat.Canonical, repository.Root);
        Assert.Contains(result.Issues, issue => issue.Code == ValidationCodes.BrokenReference);
    }

    [Fact]
    public void FencedLinksAreIgnoredAndTraversalIsRejected()
    {
        using var repository = TemporaryRepository.Create();
        var fencedPath = repository.Write("docs/adr/0001-fenced.md", Canonical("## References\n```markdown\n[Example](missing.md)\n```"));
        var traversalPath = repository.Write("docs/adr/0002-traversal.md", Canonical("## References\n[Outside](../../../outside.md)"));
        var documents = new[]
        {
            AdrMarkdownParser.Parse(fencedPath, File.ReadAllText(fencedPath)),
            AdrMarkdownParser.Parse(traversalPath, File.ReadAllText(traversalPath)),
        };
        var result = AdrValidator.Validate(documents, null, AdrFormat.Canonical, repository.Root);
        Assert.DoesNotContain(result.Issues, issue => issue.FilePath == fencedPath && issue.Code == ValidationCodes.BrokenReference);
        Assert.Contains(result.Issues, issue => issue.FilePath == traversalPath && issue.Code == ValidationCodes.BrokenReference);
    }

    private static string Canonical(string extra) => $$"""
        # Decision
        ## Status
        Accepted
        ## Context
        Context.
        ## Decision
        Decision.
        ## Consequences
        Consequences.
        {{extra}}
        """;

    private sealed class TemporaryRepository : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), $"adr-guard-links-{Guid.NewGuid():N}");
        private TemporaryRepository() => Directory.CreateDirectory(Root);
        internal static TemporaryRepository Create() => new();
        internal string Write(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
