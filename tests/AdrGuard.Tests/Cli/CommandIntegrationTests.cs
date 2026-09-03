using AdrGuard.Cli;
using Xunit;

namespace AdrGuard.Tests.Cli;

public sealed class CommandIntegrationTests
{
    [Fact]
    public void CheckValidDirectoryReturnsSuccess()
    {
        var root = CreateTempDirectory();

        try
        {
            WriteAdr(root, "0001-use-postgresql.md", ValidMarkdown("Use PostgreSQL", "Accepted"));
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.Run(["check", root], output, error);

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Contains("Validated 1 ADR(s)", output.ToString(), StringComparison.Ordinal);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void CheckInvalidDirectoryReturnsValidationFailure()
    {
        var root = CreateTempDirectory();

        try
        {
            WriteAdr(root, "0001-use-postgresql.md", ValidMarkdown("Use PostgreSQL", "Approved"));
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.Run(["check", root], output, error);

            Assert.Equal(ExitCodes.ValidationFailed, exitCode);
            Assert.Equal(string.Empty, output.ToString());
            Assert.Contains("ADR004", error.ToString(), StringComparison.Ordinal);
            Assert.Contains("Validation failed", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void CheckMissingDirectoryReturnsOperationalError()
    {
        var root = Path.Combine(Path.GetTempPath(), $"adr-guard-missing-{Guid.NewGuid():N}");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = CliApplication.Run(["check", root], output, error);

        Assert.Equal(ExitCodes.OperationalError, exitCode);
        Assert.Contains("does not exist", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void IndexWritesSortedReadmeAndIsIdempotent()
    {
        var root = CreateTempDirectory();

        try
        {
            WriteAdr(root, "0002-use-redis.md", ValidMarkdown("Use Redis", "Proposed"));
            WriteAdr(root, "0001-use-postgresql.md", ValidMarkdown("Use PostgreSQL", "Accepted"));

            using var firstOutput = new StringWriter();
            using var firstError = new StringWriter();

            var firstExitCode = CliApplication.Run(["index", root], firstOutput, firstError);

            Assert.Equal(ExitCodes.Success, firstExitCode);
            Assert.Equal(string.Empty, firstError.ToString());

            var indexPath = Path.Combine(root, "README.md");
            Assert.True(File.Exists(indexPath));

            var firstContent = File.ReadAllText(indexPath);
            var firstPosition = firstContent.IndexOf("[0001]", StringComparison.Ordinal);
            var secondPosition = firstContent.IndexOf("[0002]", StringComparison.Ordinal);

            Assert.True(firstPosition >= 0);
            Assert.True(secondPosition > firstPosition);
            Assert.Contains("[0001](0001-use-postgresql.md)", firstContent, StringComparison.Ordinal);
            Assert.Contains("Use PostgreSQL", firstContent, StringComparison.Ordinal);
            Assert.Contains("Accepted", firstContent, StringComparison.Ordinal);

            using var secondOutput = new StringWriter();
            using var secondError = new StringWriter();

            var secondExitCode = CliApplication.Run(["index", root], secondOutput, secondError);

            Assert.Equal(ExitCodes.Success, secondExitCode);
            Assert.Equal(string.Empty, secondError.ToString());
            Assert.Contains("already up to date", secondOutput.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(firstContent, File.ReadAllText(indexPath));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void IndexDoesNotWriteWhenValidationFails()
    {
        var root = CreateTempDirectory();

        try
        {
            WriteAdr(root, "0001-use-postgresql.md", ValidMarkdown("Use PostgreSQL", "Approved"));
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.Run(["index", root], output, error);

            Assert.Equal(ExitCodes.ValidationFailed, exitCode);
            Assert.False(File.Exists(Path.Combine(root, "README.md")));
            Assert.Contains("ADR004", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void IndexSupportsCustomOutputOutsideAdrDirectory()
    {
        var root = CreateTempDirectory();
        var outputPath = Path.Combine(
            Path.GetTempPath(),
            $"adr-guard-index-{Guid.NewGuid():N}.md");

        try
        {
            WriteAdr(root, "0001-use-postgresql.md", ValidMarkdown("Use PostgreSQL", "Accepted"));
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.Run(
                ["index", root, "--output", outputPath],
                output,
                error);

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.True(File.Exists(outputPath));
            Assert.False(File.Exists(Path.Combine(root, "README.md")));
        }
        finally
        {
            DeleteDirectory(root);

            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public void IndexRejectsCustomMarkdownOutputInsideAdrDirectory()
    {
        var root = CreateTempDirectory();

        try
        {
            WriteAdr(root, "0001-use-postgresql.md", ValidMarkdown("Use PostgreSQL", "Accepted"));
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CliApplication.Run(
                ["index", root, "--output", Path.Combine(root, "decisions.md")],
                output,
                error);

            Assert.Equal(ExitCodes.UsageError, exitCode);
            Assert.False(File.Exists(Path.Combine(root, "decisions.md")));
            Assert.Contains("must be named 'README.md'", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Theory]
    [InlineData("check")]
    [InlineData("index")]
    public void CommandHelpReturnsSuccess(string command)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = CliApplication.Run([command, "--help"], output, error);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains($"adr-guard {command}", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void InvalidIndexArgumentsReturnUsageError()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = CliApplication.Run(["index", "--output"], output, error);

        Assert.Equal(ExitCodes.UsageError, exitCode);
        Assert.Contains("Invalid arguments", error.ToString(), StringComparison.Ordinal);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"adr-guard-{Guid.NewGuid():N}");
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

    private static void WriteAdr(
        string directoryPath,
        string fileName,
        string markdown) =>
        File.WriteAllText(Path.Combine(directoryPath, fileName), markdown);

    private static string ValidMarkdown(string title, string status) =>
        $"""
        # {title}

        ## Status
        {status}

        ## Context
        Context.

        ## Decision
        Decision.

        ## Consequences
        Consequences.
        """;
}
