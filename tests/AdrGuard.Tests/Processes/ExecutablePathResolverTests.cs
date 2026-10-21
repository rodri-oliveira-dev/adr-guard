using AdrGuard.Processes;
using Xunit;

namespace AdrGuard.Tests.Processes;

public sealed class ExecutablePathResolverTests
{
    [Fact]
    public void ResolveReturnsAnAbsoluteExecutablePath()
    {
        var root = CreateTempDirectory();
        try
        {
            var fileName = OperatingSystem.IsWindows() ? "tool.exe" : "tool";
            var executablePath = Path.Combine(root, fileName);
            File.WriteAllText(executablePath, string.Empty);

            var result = ExecutablePathResolver.Resolve("tool", root);

            Assert.Equal(executablePath, result);
            Assert.True(Path.IsPathFullyQualified(result));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ResolveIgnoresRelativeSearchDirectories()
    {
        var exception = Assert.Throws<FileNotFoundException>(
            () => ExecutablePathResolver.Resolve("tool", "relative-directory"));

        Assert.Contains("Unable to locate", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../tool")]
    public void ResolveRejectsInvalidExecutableNames(string executableName)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => ExecutablePathResolver.Resolve(executableName, Path.GetTempPath()));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"adr-guard-executable-path-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
