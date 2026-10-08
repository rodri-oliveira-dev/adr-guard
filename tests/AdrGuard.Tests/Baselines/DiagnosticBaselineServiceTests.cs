using AdrGuard.Baselines;
using AdrGuard.Validation;
using Xunit;

namespace AdrGuard.Tests.Baselines;

public sealed class DiagnosticBaselineServiceTests
{
    [Fact]
    public void CreateSerializeLoadAndCompareAreDeterministic()
    {
        var root = CreateTempDirectory();
        try
        {
            var issue = Issue(root, "0001-old.md", ValidationCodes.MissingSection, "Missing Decision.");
            var result = new ValidationResult([issue]);
            var baseline = DiagnosticBaselineService.Create(result, root);
            Assert.Equal(
                DiagnosticBaselineService.Serialize(baseline),
                DiagnosticBaselineService.Serialize(DiagnosticBaselineService.Create(result, root)));

            var path = Path.Combine(root, "baseline.json");
            DiagnosticBaselineService.Write(path, baseline, update: false);
            var comparison = DiagnosticBaselineService.Compare(
                result,
                DiagnosticBaselineService.Load(path),
                root);

            Assert.Empty(comparison.NewIssues);
            Assert.Single(comparison.ExistingIssues);
            Assert.Empty(comparison.ResolvedEntries);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RenameChangeDeleteAndReappearanceAreClassifiedPredictably()
    {
        var root = CreateTempDirectory();
        try
        {
            var original = Issue(root, "0001-old.md", ValidationCodes.MissingSection, "Missing Decision.");
            var baseline = DiagnosticBaselineService.Create(new ValidationResult([original]), root);

            var renamed = Issue(root, "0001-renamed.md", original.Code, original.Message);
            var renameComparison = DiagnosticBaselineService.Compare(
                new ValidationResult([renamed]), baseline, root);
            Assert.Single(renameComparison.NewIssues);
            Assert.Single(renameComparison.ResolvedEntries);

            var changed = Issue(root, "0001-old.md", original.Code, "Missing Context.");
            Assert.Single(DiagnosticBaselineService.Compare(
                new ValidationResult([changed]), baseline, root).NewIssues);
            Assert.Single(DiagnosticBaselineService.Compare(
                new ValidationResult([]), baseline, root).ResolvedEntries);

            var reappeared = DiagnosticBaselineService.Compare(
                new ValidationResult([original]), baseline, root);
            Assert.Empty(reappeared.NewIssues);
            Assert.Single(reappeared.ExistingIssues);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CreatingBaselineDeduplicatesIdenticalDiagnosticFingerprints()
    {
        var root = CreateTempDirectory();
        try
        {
            var issue = Issue(root, "0001-legacy.md", ValidationCodes.BrokenReference,
                "Reference 'missing.md' does not resolve.");
            var baseline = DiagnosticBaselineService.Create(
                new ValidationResult([issue, issue]), root);

            Assert.Single(baseline.Diagnostics);
            var text = DiagnosticBaselineService.Serialize(baseline);
            Assert.Contains(issue.Code, text, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DuplicateCanonicalSectionIsDocumentLocalAndCanBeBaselined()
    {
        var root = CreateTempDirectory();
        try
        {
            var issue = Issue(root, "0001-legacy.md",
                ValidationCodes.DuplicateCanonicalSection, "Duplicate Status section.");
            var current = new ValidationResult([issue]);
            var comparison = DiagnosticBaselineService.Compare(
                current, DiagnosticBaselineService.Create(current, root), root);

            Assert.Empty(comparison.NewIssues);
            Assert.Single(comparison.ExistingIssues);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GlobalDiagnosticsCannotBeSuppressedByBaseline()
    {
        var root = CreateTempDirectory();
        try
        {
            var issue = Issue(root, "0001-old.md", ValidationCodes.DuplicateId, "Duplicate.");
            var result = new ValidationResult([issue]);
            var comparison = DiagnosticBaselineService.Compare(
                result,
                DiagnosticBaselineService.Create(result, root),
                root);

            Assert.Single(comparison.NewIssues);
            Assert.Empty(comparison.ExistingIssues);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":\"2.0\",\"diagnostics\":[]}")]
    [InlineData("{\"schemaVersion\":\"1.0\",\"diagnostics\":[],\"unknown\":true}")]
    public void LoadRejectsInvalidOrIncompatibleBaseline(string content)
    {
        var root = CreateTempDirectory();
        try
        {
            var path = Path.Combine(root, "baseline.json");
            File.WriteAllText(path, content);
            Assert.ThrowsAny<InvalidDataException>(() => DiagnosticBaselineService.Load(path));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static ValidationIssue Issue(string root, string file, string code, string message) =>
        new(code, Path.Combine(root, file), message);

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"adr-guard-baseline-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }
}
