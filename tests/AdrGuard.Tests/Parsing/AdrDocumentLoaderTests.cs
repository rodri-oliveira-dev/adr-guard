using AdrGuard.Parsing;
using Xunit;

namespace AdrGuard.Tests.Parsing;

public sealed class AdrDocumentLoaderTests
{
    [Fact]
    public void LoadDirectoryObservesCallerCancellation()
    {
        var root = CreateTempDirectory();

        try
        {
            File.WriteAllText(
                Path.Combine(
                    root,
                    "0001-use-redis.md"),
                """
                # Use Redis

                ## Status
                Accepted

                ## Context
                Context.

                ## Decision
                Decision.

                ## Consequences
                Consequences.
                """);

            using var cancellationSource =
                new CancellationTokenSource();

            cancellationSource.Cancel();

            Assert.ThrowsAny<OperationCanceledException>(
                () => AdrDocumentLoader.LoadDirectory(
                    root,
                    cancellationSource.Token));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void LoadBoundedDirectoryObservesCancellationBeforeEnumeration()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cancellationSource = new CancellationTokenSource();
            cancellationSource.Cancel();

            Assert.ThrowsAny<OperationCanceledException>(() => AdrDocumentLoader.LoadBoundedDirectory(
                root,
                root,
                cancellationSource.Token));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void LoadBoundedDirectoryRejectsTheFirstFileBeyondItsLimit()
    {
        var root = CreateTempDirectory();
        try
        {
            for (var index = 0; index <= AdrDocumentLoader.MaximumBoundedAdrFiles; index++)
            {
                File.WriteAllText(Path.Combine(root, $"{index:D4}-decision.md"), "# Decision");
            }

            var exception = Assert.Throws<InvalidDataException>(() => AdrDocumentLoader.LoadBoundedDirectory(
                root,
                root,
                TestContext.Current.CancellationToken));

            Assert.Contains("file input limit", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"adr-guard-loader-{Guid.NewGuid():N}");

        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(
        string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(
                path,
                recursive: true);
        }
    }
}
