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
