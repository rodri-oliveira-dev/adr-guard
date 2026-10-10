using AdrGuard.Git;
using Xunit;

namespace AdrGuard.Tests.Git;

public sealed class GitChangeInventoryTests
{
    [Fact]
    public void ReadsCommittedStagedUnstagedUntrackedRenameDeleteAndCopyChanges()
    {
        using var repository = TemporaryGitRepository.Create();
        repository.Run("switch", "-c", "feature");
        repository.Append("src/modified.txt", "committed\n");
        repository.Write("src/added.txt", "added\n");
        repository.Delete("src/deleted.txt");
        repository.Run("mv", "src/rename-source.txt", "src/renamed.txt");
        repository.Copy("src/copy-source.txt", "src/copied.txt");
        repository.Run("add", ".");
        repository.Run("commit", "-m", "feature changes");

        repository.Write("src/staged.txt", "staged\n");
        repository.Run("add", "src/staged.txt");
        repository.Append("src/unstaged.txt", "unstaged\n");
        repository.Write("src/untracked file.txt", "untracked\n");

        var inventory = GitChangeInventoryReader.Read(
            repository.Root,
            "main",
            TestContext.Current.CancellationToken);

        Assert.Equal(repository.Root, inventory.RepositoryRoot);
        Assert.False(string.IsNullOrWhiteSpace(inventory.MergeBase));
        Assert.Contains(inventory.Changes, change =>
            change.Scope == GitChangeScope.Committed
            && change.Kind == GitChangeKind.Modified
            && change.NewPath == "src/modified.txt");
        Assert.Contains(inventory.Changes, change =>
            change.Scope == GitChangeScope.Committed
            && change.Kind == GitChangeKind.Added
            && change.NewPath == "src/added.txt");
        Assert.Contains(inventory.Changes, change =>
            change.Scope == GitChangeScope.Committed
            && change.Kind == GitChangeKind.Deleted
            && change.OldPath == "src/deleted.txt");
        Assert.Contains(inventory.Changes, change =>
            change.Scope == GitChangeScope.Committed
            && change.Kind == GitChangeKind.Renamed
            && change.OldPath == "src/rename-source.txt"
            && change.NewPath == "src/renamed.txt"
            && change.Similarity is > 0);
        Assert.Contains(inventory.Changes, change =>
            change.Scope == GitChangeScope.Committed
            && change.Kind == GitChangeKind.Copied
            && change.OldPath == "src/copy-source.txt"
            && change.NewPath == "src/copied.txt");
        Assert.Contains(inventory.Changes, change =>
            change.Scope == GitChangeScope.Staged
            && change.NewPath == "src/staged.txt");
        Assert.Contains(inventory.Changes, change =>
            change.Scope == GitChangeScope.Unstaged
            && change.NewPath == "src/unstaged.txt");
        Assert.Contains(inventory.Changes, change =>
            change.Scope == GitChangeScope.Untracked
            && change.NewPath == "src/untracked file.txt");

        Assert.Equal(
            inventory.Changes.OrderBy(change => change.NewPath ?? change.OldPath, StringComparer.Ordinal)
                .ThenBy(change => change.OldPath, StringComparer.Ordinal)
                .ThenBy(change => change.Scope)
                .ThenBy(change => change.Kind)
                .ThenBy(change => change.Similarity),
            inventory.Changes);
    }

    [Fact]
    public void DetachedHeadUsesExplicitBaseWithoutChangingRepositoryState()
    {
        using var repository = TemporaryGitRepository.Create();
        repository.Run("switch", "--detach");
        repository.Append("src/modified.txt", "detached\n");

        var before = repository.RunOutput("rev-parse", "HEAD").Trim();
        var inventory = GitChangeInventoryReader.Read(
            repository.Root,
            "main",
            TestContext.Current.CancellationToken);
        var after = repository.RunOutput("rev-parse", "HEAD").Trim();

        Assert.Equal(before, after);
        Assert.Contains(inventory.Changes, change => change.NewPath == "src/modified.txt");
    }

    [Fact]
    public void MissingMergeBaseIsOperationalFailureNotEmptyInventory()
    {
        using var repository = TemporaryGitRepository.Create();
        repository.Run("switch", "--orphan", "disconnected");
        repository.Write("disconnected.txt", "content\n");
        repository.Run("add", ".");
        repository.Run("commit", "-m", "disconnected");

        var exception = Assert.Throws<GitOperationException>(
            () => GitChangeInventoryReader.Read(
                repository.Root,
                "main",
                TestContext.Current.CancellationToken));

        Assert.Contains("Git exited", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShallowCloneWithMissingBaseHistoryFailsExplicitly()
    {
        using var repository = TemporaryGitRepository.Create();
        repository.Append("src/modified.txt", "second commit\n");
        repository.Run("add", ".");
        repository.Run("commit", "-m", "second");
        var cloneParent = Path.Combine(
            Path.GetTempPath(),
            $"adr-impact-shallow-{Guid.NewGuid():N}");
        var clone = Path.Combine(cloneParent, "clone");
        Directory.CreateDirectory(cloneParent);

        try
        {
            GitCommandRunner.Run(
                cloneParent,
                ["clone", "--depth", "1", new Uri(repository.Root).AbsoluteUri, clone],
                TestContext.Current.CancellationToken);

            var exception = Assert.Throws<GitOperationException>(
                () => GitChangeInventoryReader.Read(
                    clone,
                    "HEAD~1",
                    TestContext.Current.CancellationToken));

            Assert.Contains("Git exited", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            foreach (var path in Directory.EnumerateFiles(cloneParent, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }

            Directory.Delete(cloneParent, recursive: true);
        }
    }

    [Theory]
    [InlineData("--upload-pack=evil")]
    [InlineData("main injected")]
    [InlineData("main\nnext")]
    public void RejectsMaliciousBaseReferenceBeforeGitExecution(string reference)
    {
        Assert.Throws<ArgumentException>(
            () => GitChangeInventoryReader.Read(
                ".",
                reference,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ParsesNullDelimitedSpecialNamesAndRejectsMalformedRecords()
    {
        var changes = GitChangeInventoryReader.ParseNameStatus(
            "R087\0src/old name.txt\0src/new\nname.txt\0D\0src/deleted.txt\0",
            GitChangeScope.Committed);

        var rename = Assert.Single(changes, change => change.Kind == GitChangeKind.Renamed);
        Assert.Equal(87, rename.Similarity);
        Assert.Equal("src/new\nname.txt", rename.NewPath);
        Assert.Contains(changes, change =>
            change.Kind == GitChangeKind.Deleted
            && change.OldPath == "src/deleted.txt");

        Assert.Throws<GitOperationException>(
            () => GitChangeInventoryReader.ParseNameStatus("R100\0old\0", GitChangeScope.Committed));
        Assert.Throws<GitOperationException>(
            () => GitChangeInventoryReader.ParseNameStatus("X\0path\0", GitChangeScope.Committed));
        Assert.Throws<GitOperationException>(
            () => GitChangeInventoryReader.ParseNameStatus("M\0../outside\0", GitChangeScope.Committed));
    }

    [Fact]
    public void EnforcesEntryBoundAndCancellation()
    {
        var output = string.Concat(
            Enumerable.Range(0, GitChangeInventoryReader.MaximumChanges + 1)
                .Select(index => $"A\0src/{index:D5}.txt\0"));

        Assert.Contains(
            "entry limit",
            Assert.Throws<GitOperationException>(
                () => GitChangeInventoryReader.ParseNameStatus(output, GitChangeScope.Committed)).Message,
            StringComparison.Ordinal);

        using var repository = TemporaryGitRepository.Create();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(
            () => GitChangeInventoryReader.Read(repository.Root, "main", cancellation.Token));
    }

    [Fact]
    public void BoundedRunnerRejectsExcessOutputAndCaseDistinctPathsStayDistinct()
    {
        using var repository = TemporaryGitRepository.Create();
        Assert.Contains(
            "output exceeded",
            Assert.Throws<GitOperationException>(
                () => GitCommandRunner.RunBounded(
                    repository.Root,
                    ["show", "--format=fuller", "HEAD"],
                    TimeSpan.FromSeconds(5),
                    10,
                    TestContext.Current.CancellationToken)).Message,
            StringComparison.Ordinal);

        if (OperatingSystem.IsWindows())
        {
            return;
        }

        repository.Write("src/Case.txt", "upper\n");
        repository.Write("src/case.txt", "lower\n");
        var inventory = GitChangeInventoryReader.Read(
            repository.Root,
            "main",
            TestContext.Current.CancellationToken);

        Assert.Contains(inventory.Changes, change => change.NewPath == "src/Case.txt");
        Assert.Contains(inventory.Changes, change => change.NewPath == "src/case.txt");
    }

    [Fact]
    public void ExistingAdrScopedDetectorRetainsPublishedBehavior()
    {
        using var repository = TemporaryGitRepository.Create();
        repository.Write("docs/adr/0001-one.md", "# One\n");
        repository.Run("add", ".");
        repository.Run("commit", "-m", "add adr");
        repository.Append("docs/adr/0001-one.md", "changed\n");
        repository.Write("src/not-an-adr.md", "# Documentation\n");

        var changes = GitChangeDetector.Detect(
            Path.Combine(repository.Root, "docs", "adr"),
            "main",
            TestContext.Current.CancellationToken);

        Assert.Contains(Path.Combine(repository.Root, "docs", "adr", "0001-one.md"), changes.CurrentPaths);
        Assert.DoesNotContain(Path.Combine(repository.Root, "src", "not-an-adr.md"), changes.CurrentPaths);
    }

    private sealed class TemporaryGitRepository : IDisposable
    {
        internal string Root { get; } = Path.Combine(
            Path.GetTempPath(),
            $"adr-impact-git-{Guid.NewGuid():N}");

        private TemporaryGitRepository()
        {
            Directory.CreateDirectory(Path.Combine(Root, "src"));
            Run("init", "--initial-branch=main");
            Run("config", "user.email", "tests@example.invalid");
            Run("config", "user.name", "ADR Guard Tests");
            Write("src/modified.txt", "base\n");
            Write("src/deleted.txt", "delete\n");
            Write("src/rename-source.txt", "rename content\n");
            Write("src/copy-source.txt", "copy content\n");
            Write("src/unstaged.txt", "base\n");
            Run("add", ".");
            Run("commit", "-m", "base");
        }

        internal static TemporaryGitRepository Create() => new();

        internal void Write(string relativePath, string content)
        {
            var path = Resolve(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        internal void Append(string relativePath, string content) =>
            File.AppendAllText(Resolve(relativePath), content);

        internal void Delete(string relativePath) => File.Delete(Resolve(relativePath));

        internal void Copy(string source, string destination) =>
            File.Copy(Resolve(source), Resolve(destination));

        internal void Run(params string[] arguments) =>
            GitCommandRunner.Run(Root, arguments, default);

        internal string RunOutput(params string[] arguments) =>
            GitCommandRunner.Run(Root, arguments, default);

        private string Resolve(string relativePath) =>
            Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

        public void Dispose()
        {
            foreach (var path in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }

            Directory.Delete(Root, recursive: true);
        }
    }
}
