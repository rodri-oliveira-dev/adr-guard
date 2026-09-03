using AdrGuard.Generation;
using System.Globalization;
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

            await File.WriteAllTextAsync(
                secondPath,
                "second content",
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                firstPath,
                "first content",
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                unselectedPath,
                "must never be read",
                TestContext.Current.CancellationToken);

            var files = await ExplicitContextFileLoader.LoadAsync(
                [secondPath, firstPath],
                TestContext.Current.CancellationToken);

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
    public async Task LoadAsyncAcceptsFileExactlyAtPerFileCharacterLimit()
    {
        var root = CreateTempDirectory();

        try
        {
            var filePath = Path.Combine(root, "context.txt");
            var content = new string(
                'x',
                AdrGenerationContextLimits
                    .MaximumContextFileCharacters);

            await File.WriteAllTextAsync(
                filePath,
                content,
                TestContext.Current.CancellationToken);

            var files = await ExplicitContextFileLoader.LoadAsync(
                [filePath],
                TestContext.Current.CancellationToken);

            var file = Assert.Single(files);
            Assert.Equal(content.Length, file.Content.Length);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task LoadAsyncRejectsFileOneCharacterOverPerFileLimit()
    {
        var root = CreateTempDirectory();

        try
        {
            var filePath = Path.Combine(root, "oversized.txt");
            var sensitiveTail = "SENSITIVE-TAIL";
            var content = new string(
                'x',
                AdrGenerationContextLimits
                    .MaximumContextFileCharacters
                + 1)
                + sensitiveTail;

            await File.WriteAllTextAsync(
                filePath,
                content,
                TestContext.Current.CancellationToken);

            var exception =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => ExplicitContextFileLoader.LoadAsync(
                        [filePath],
                        TestContext.Current.CancellationToken));

            Assert.Contains(
                Path.GetFullPath(filePath),
                exception.Message,
                StringComparison.Ordinal);
            Assert.Contains(
                AdrGenerationContextLimits
                    .MaximumContextFileCharacters
                    .ToString(CultureInfo.InvariantCulture),
                exception.Message,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                sensitiveTail,
                exception.Message,
                StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task LoadAsyncAcceptsAggregateExactlyAtLimit()
    {
        var root = CreateTempDirectory();

        try
        {
            var firstPath = Path.Combine(root, "first.txt");
            var secondPath = Path.Combine(root, "second.md");

            await File.WriteAllTextAsync(
                firstPath,
                new string(
                    'a',
                    AdrGenerationContextLimits
                        .MaximumContextFileCharacters),
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                secondPath,
                new string(
                    'b',
                    AdrGenerationContextLimits
                        .MaximumContextFileCharacters),
                TestContext.Current.CancellationToken);

            var files = await ExplicitContextFileLoader.LoadAsync(
                [firstPath, secondPath],
                TestContext.Current.CancellationToken);

            Assert.Equal(2, files.Count);
            Assert.Equal(
                AdrGenerationContextLimits
                    .MaximumAggregateContextFileCharacters,
                files.Sum(file => file.Content.Length));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task LoadAsyncRejectsAggregateContextFileOverflow()
    {
        var root = CreateTempDirectory();

        try
        {
            var firstPath = Path.Combine(root, "first.txt");
            var secondPath = Path.Combine(root, "second.md");
            var thirdPath = Path.Combine(root, "third.txt");

            await File.WriteAllTextAsync(
                firstPath,
                new string('a', 40000),
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                secondPath,
                new string('b', 40000),
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                thirdPath,
                new string('c', 20001),
                TestContext.Current.CancellationToken);

            var exception =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => ExplicitContextFileLoader.LoadAsync(
                        [firstPath, secondPath, thirdPath],
                        TestContext.Current.CancellationToken));

            Assert.Contains(
                "aggregate limit",
                exception.Message,
                StringComparison.Ordinal);
            Assert.Contains(
                AdrGenerationContextLimits
                    .MaximumAggregateContextFileCharacters
                    .ToString(CultureInfo.InvariantCulture),
                exception.Message,
                StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task LoadAsyncObservesCallerCancellation()
    {
        var root = CreateTempDirectory();

        try
        {
            var filePath = Path.Combine(
                root,
                "context.txt");

            await File.WriteAllTextAsync(
                filePath,
                "context",
                TestContext.Current.CancellationToken);

            using var cancellationSource =
                new CancellationTokenSource();

            cancellationSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => ExplicitContextFileLoader.LoadAsync(
                    [filePath],
                    cancellationSource.Token));
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
            await File.WriteAllTextAsync(
                filePath,
                "{}",
                TestContext.Current.CancellationToken);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => ExplicitContextFileLoader.LoadAsync(
                    [filePath],
                    TestContext.Current.CancellationToken));

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
                    TestContext.Current.CancellationToken));

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
