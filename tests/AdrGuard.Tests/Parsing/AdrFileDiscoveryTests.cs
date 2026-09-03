using AdrGuard.Parsing;
using Xunit;

namespace AdrGuard.Tests.Parsing;

public sealed class AdrFileDiscoveryTests
{
    [Fact]
    public void FindMarkdownFilesReturnsMarkdownFilesRecursivelyInOrdinalOrder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"adr-guard-{Guid.NewGuid():N}");

        try
        {
            var nested = Path.Combine(root, "nested");
            Directory.CreateDirectory(nested);

            var second = Path.Combine(root, "0002-second.md");
            var first = Path.Combine(nested, "0001-first.md");
            var uppercaseExtension = Path.Combine(root, "0003-third.MD");
            var ignored = Path.Combine(root, "notes.txt");

            File.WriteAllText(second, "# Second");
            File.WriteAllText(first, "# First");
            File.WriteAllText(uppercaseExtension, "# Third");
            File.WriteAllText(ignored, "ignore");

            var files = AdrFileDiscovery.FindMarkdownFiles(root);

            Assert.Equal(3, files.Count);
            Assert.Equal(files.Order(StringComparer.Ordinal), files);
            Assert.Contains(first, files);
            Assert.Contains(second, files);
            Assert.Contains(uppercaseExtension, files);
            Assert.DoesNotContain(ignored, files);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void FindMarkdownFilesReturnsEmptyCollectionForEmptyDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"adr-guard-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(root);

            var files = AdrFileDiscovery.FindMarkdownFiles(root);

            Assert.Empty(files);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
