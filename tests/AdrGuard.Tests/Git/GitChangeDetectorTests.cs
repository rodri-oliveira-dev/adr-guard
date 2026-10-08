using AdrGuard.Git;
using Xunit;

namespace AdrGuard.Tests.Git;

public sealed class GitChangeDetectorTests
{
    [Fact]
    public void DetectFindsAddedModifiedRenamedRemovedAndUntrackedAdrs()
    {
        var root = CreateRepository();

        try
        {
            var adr = Path.Combine(root, "docs", "adr");
            File.AppendAllText(Path.Combine(adr, "0001-first.md"), "\nModified.\n");
            RunGit(root, "mv", "docs/adr/0002-second.md", "docs/adr/0005-renamed.md");
            File.Delete(Path.Combine(adr, "0003-third.md"));
            File.WriteAllText(Path.Combine(adr, "0004-untracked.md"), "# Untracked");

            var changes = GitChangeDetector.Detect(
                adr,
                "main",
                TestContext.Current.CancellationToken);

            Assert.Contains(Path.Combine(adr, "0001-first.md"), changes.CurrentPaths);
            Assert.Contains(Path.Combine(adr, "0005-renamed.md"), changes.CurrentPaths);
            Assert.Contains(Path.Combine(adr, "0004-untracked.md"), changes.CurrentPaths);
            Assert.Contains(Path.Combine(adr, "0002-second.md"), changes.RemovedPaths);
            Assert.Contains(Path.Combine(adr, "0003-third.md"), changes.RemovedPaths);
        }
        finally
        {
            DeleteRepository(root);
        }
    }

    [Theory]
    [InlineData("--upload-pack=evil")]
    [InlineData("main injected")]
    [InlineData("main\nnext")]
    public void DetectRejectsUnsafeReferenceBeforeGitExecution(string reference)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => GitChangeDetector.Detect(
                ".",
                reference,
                TestContext.Current.CancellationToken));

        Assert.Contains("unsafe", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateRepository()
    {
        var root = Path.Combine(Path.GetTempPath(), $"adr-guard-git-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "docs", "adr"));
        RunGit(root, "init", "--initial-branch=main");
        RunGit(root, "config", "user.email", "tests@example.invalid");
        RunGit(root, "config", "user.name", "ADR Guard Tests");
        foreach (var id in Enumerable.Range(1, 3))
        {
            File.WriteAllText(
                Path.Combine(root, "docs", "adr", $"{id:D4}-{"first second third".Split(' ')[id - 1]}.md"),
                $"# Decision {id}");
        }

        RunGit(root, "add", ".");
        RunGit(root, "commit", "-m", "base");
        return root;
    }

    private static void RunGit(string root, params string[] arguments) =>
        GitCommandRunner.Run(root, arguments, default);

    private static void DeleteRepository(string root)
    {
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }

        Directory.Delete(root, recursive: true);
    }
}
