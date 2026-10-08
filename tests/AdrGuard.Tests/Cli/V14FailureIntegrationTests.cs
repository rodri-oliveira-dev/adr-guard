using AdrGuard.Cli;
using AdrGuard.Review;
using Xunit;

namespace AdrGuard.Tests.Cli;

public sealed class V14FailureIntegrationTests
{
    [Fact]
    public void CanceledIncrementalCheckIsOperationalFailure()
    {
        var root = CreateTempDirectory();
        try
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.Run(
                ["check", root, "--changed", "--base-ref", "main"],
                output,
                error,
                cancellation.Token);

            Assert.Equal(ExitCodes.OperationalError, exitCode);
            Assert.Contains("canceled", error.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CanceledBaselineWriteIsOperationalFailureAndWritesNothing()
    {
        var root = CreateTempDirectory();
        try
        {
            var outputPath = Path.Combine(root, "baseline.json");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.Run(
                ["baseline", root, "--output", outputPath],
                output,
                error,
                cancellation.Token);

            Assert.Equal(ExitCodes.OperationalError, exitCode);
            Assert.False(File.Exists(outputPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"adr-guard-v14-failure-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }
}
