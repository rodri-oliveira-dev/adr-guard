using AdrGuard.Generation;
using Xunit;

namespace AdrGuard.Tests.Generation;

public sealed class AtomicAdrDraftFilePersistenceTests
{
    [Fact]
    public async Task WriteNewAsyncPromotesOnlyAfterCompleteTemporaryWrite()
    {
        var root = CreateTempDirectory();

        try
        {
            var finalPath = Path.Combine(
                root,
                "0001-use-redis.md");
            const string content = "complete ADR content";
            var temporaryWriteCompleted = false;

            var persistence =
                new AtomicAdrDraftFilePersistence(
                    async (
                        temporaryPath,
                        value,
                        cancellationToken) =>
                    {
                        Assert.False(File.Exists(finalPath));

                        await File.WriteAllTextAsync(
                            temporaryPath,
                            value,
                            cancellationToken);

                        Assert.False(File.Exists(finalPath));
                        temporaryWriteCompleted = true;
                    },
                    (temporaryPath, destinationPath) =>
                    {
                        Assert.True(temporaryWriteCompleted);
                        Assert.False(File.Exists(destinationPath));

                        File.Move(
                            temporaryPath,
                            destinationPath);
                    });

            await persistence.WriteNewAsync(
                finalPath,
                content,
                TestContext.Current.CancellationToken);

            Assert.True(File.Exists(finalPath));
            Assert.Equal(
                content,
                await File.ReadAllTextAsync(
                    finalPath,
                    TestContext.Current.CancellationToken));
            AssertNoTemporaryFiles(root);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task WriteNewAsyncCancellationAfterTemporaryWriteCleansUp()
    {
        var root = CreateTempDirectory();

        try
        {
            var finalPath = Path.Combine(
                root,
                "0001-use-redis.md");

            using var cancellationSource =
                new CancellationTokenSource();

            var persistence =
                new AtomicAdrDraftFilePersistence(
                    async (
                        temporaryPath,
                        value,
                        cancellationToken) =>
                    {
                        await File.WriteAllTextAsync(
                            temporaryPath,
                            "partial",
                            CancellationToken.None);

                        cancellationSource.Cancel();
                        cancellationToken.ThrowIfCancellationRequested();
                    });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => persistence.WriteNewAsync(
                    finalPath,
                    "complete",
                    cancellationSource.Token));

            Assert.False(File.Exists(finalPath));
            AssertNoTemporaryFiles(root);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task WriteNewAsyncIoFailureCleansUpTemporaryFile()
    {
        var root = CreateTempDirectory();

        try
        {
            var finalPath = Path.Combine(
                root,
                "0001-use-redis.md");

            var persistence =
                new AtomicAdrDraftFilePersistence(
                    async (
                        temporaryPath,
                        value,
                        cancellationToken) =>
                    {
                        await File.WriteAllTextAsync(
                            temporaryPath,
                            "partial",
                            cancellationToken);

                        throw new IOException(
                            "simulated write failure");
                    });

            await Assert.ThrowsAsync<IOException>(
                () => persistence.WriteNewAsync(
                    finalPath,
                    "complete",
                    TestContext.Current.CancellationToken));

            Assert.False(File.Exists(finalPath));
            AssertNoTemporaryFiles(root);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task WriteNewAsyncRaceNeverOverwritesCompetingFinalFile()
    {
        var root = CreateTempDirectory();

        try
        {
            var finalPath = Path.Combine(
                root,
                "0001-use-redis.md");
            const string competingContent =
                "concurrently created ADR";

            var persistence =
                new AtomicAdrDraftFilePersistence(
                    promoteTemporaryFile:
                        (temporaryPath, destinationPath) =>
                        {
                            File.WriteAllText(
                                destinationPath,
                                competingContent);

                            File.Move(
                                temporaryPath,
                                destinationPath);
                        });

            var exception =
                await Assert.ThrowsAsync<IOException>(
                    () => persistence.WriteNewAsync(
                        finalPath,
                        "generated ADR",
                        TestContext.Current.CancellationToken));

            Assert.Contains(
                "already exists",
                exception.Message,
                StringComparison.OrdinalIgnoreCase);
            Assert.Equal(
                competingContent,
                File.ReadAllText(finalPath));
            AssertNoTemporaryFiles(root);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static void AssertNoTemporaryFiles(
        string directoryPath)
    {
        Assert.DoesNotContain(
            Directory.EnumerateFiles(directoryPath),
            filePath =>
                Path.GetFileName(filePath)
                    .EndsWith(
                        AtomicAdrDraftFilePersistence
                            .TemporaryFileSuffix,
                        StringComparison.Ordinal));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"adr-guard-atomic-persistence-{Guid.NewGuid():N}");

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
