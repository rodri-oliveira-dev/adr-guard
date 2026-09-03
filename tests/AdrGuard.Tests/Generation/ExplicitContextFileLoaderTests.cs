using AdrGuard.Generation;
using Xunit;

namespace AdrGuard.Tests.Generation;

public sealed class ExplicitContextFileLoaderTests
{
    [Fact]
    public async Task LoadAsyncReadsOnlyExplicitFilesInSuppliedOrder()
    {
        var root = CreateTempDirectory();

        try
        {
            var secondPath = Path.Combine(root, "second.txt");
            var firstPath = Path.Combine(root, "first.md");
            var unselectedPath = Path.Combine(root, "unselected.txt");

            await File.WriteAllTextAsync(secondPath, "second content");
            await File.WriteAllTextAsync(firstPath, "first content");
            await File.WriteAllTextAsync(unselectedPath, "must never be read");

            var files = await ExplicitContextFileLoader.LoadAsync(
                [secondPath, firstPath],
                CancellationToken.None);

            Assert.Collection(
                files,
                file =>
                {
                    Assert.Equal(Path.GetFullPath(secondPath), file.FilePath);
                    Assert.Equal("second content", file.Content);
                },
                file =>
                {
                    Assert.Equal(Path.GetFullPath(firstPath), file.FilePath);
                    Assert.Equal("first content", file.Content);
                });

            Assert.DoesNotContain(
                files,
                file => string.Equals(
                    file.FilePath,
                    Path.GetFullPath(unselectedPath),
                    StringComparison.Ordinal));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task LoadAsyncRejectsUnsupportedExtensionsBeforeReading()
    {
        var root = CreateTempDirectory();

        try
        {
            var filePath = Path.Combine(root, "context.json");
            await File.WriteAllTextAsync(filePath, "{}");

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => ExplicitContextFileLoader.LoadAsync(
                    [filePath],
                    CancellationToken.None));

            Assert.Contains(
                "Only .md and .txt files are supported",
                exception.Message,
                StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task LoadAsyncRejectsMissingExplicitFile()
    {
        var root = CreateTempDirectory();

        try
        {
            var missingPath = Path.Combine(root, "missing.md");

            var exception = await Assert.ThrowsAsync<FileNotFoundException>(
                () => ExplicitContextFileLoader.LoadAsync(
                    [missingPath],
                    CancellationToken.None));

            Assert.Equal(Path.GetFullPath(missingPath), exception.FileName);
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
            $"adr-guard-context-file-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
