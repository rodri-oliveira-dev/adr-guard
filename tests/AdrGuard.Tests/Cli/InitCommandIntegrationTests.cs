using AdrGuard.Cli;
using Xunit;

namespace AdrGuard.Tests.Cli;

public sealed class InitCommandIntegrationTests
{
    [Fact]
    public void InitCreatesConfigurationDirectoryAndOptionalWorkflowIdempotently()
    {
        var root = CreateTempDirectory();

        try
        {
            var first = Run(["init", root, "--template", "extended", "--github-actions"]);
            var configPath = Path.Combine(root, ".adrguard.yml");
            var workflowPath = Path.Combine(root, ".github", "workflows", "adr-guard.yml");

            Assert.Equal(ExitCodes.Success, first.ExitCode);
            Assert.True(Directory.Exists(Path.Combine(root, "docs", "adr")));
            Assert.Contains("template: extended", File.ReadAllText(configPath), StringComparison.Ordinal);
            Assert.Contains("permissions:\n  contents: read", File.ReadAllText(workflowPath), StringComparison.Ordinal);

            var configTimestamp = File.GetLastWriteTimeUtc(configPath);
            var second = Run(["init", root, "--template", "extended", "--github-actions"]);

            Assert.Equal(ExitCodes.Success, second.ExitCode);
            Assert.Contains("Unchanged file", second.Output, StringComparison.Ordinal);
            Assert.Equal(configTimestamp, File.GetLastWriteTimeUtc(configPath));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void InitDryRunWritesNothing()
    {
        var root = CreateTempDirectory();

        try
        {
            var result = Run(["init", root, "--dry-run", "--github-actions"]);

            Assert.Equal(ExitCodes.Success, result.ExitCode);
            Assert.Contains("Would create", result.Output, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(root, ".adrguard.yml")));
            Assert.False(Directory.Exists(Path.Combine(root, "docs")));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void InitDoesNotOverwriteWithoutExplicitAuthorization()
    {
        var root = CreateTempDirectory();

        try
        {
            var configPath = Path.Combine(root, ".adrguard.yml");
            File.WriteAllText(configPath, "owned: by-user\n");

            var refused = Run(["init", root]);
            Assert.Equal(ExitCodes.OperationalError, refused.ExitCode);
            Assert.Contains("Refusing to overwrite", refused.Error, StringComparison.Ordinal);
            Assert.Equal("owned: by-user\n", File.ReadAllText(configPath));

            var allowed = Run(["init", root, "--overwrite"]);
            Assert.Equal(ExitCodes.Success, allowed.ExitCode);
            Assert.Contains("schema-version: 1", File.ReadAllText(configPath), StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("../../docs/adr")]
    public void InitRejectsTraversal(string path)
    {
        var root = CreateTempDirectory();

        try
        {
            var result = Run(["init", root, "--adr-directory", path]);

            Assert.Equal(ExitCodes.UsageError, result.ExitCode);
            Assert.Contains("inside the repository", result.Error, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(root, ".adrguard.yml")));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void InitRejectsAbsoluteManagedPath()
    {
        var root = CreateTempDirectory();

        try
        {
            var result = Run(["init", root, "--adr-directory", Path.GetTempPath()]);

            Assert.Equal(ExitCodes.UsageError, result.ExitCode);
            Assert.Contains("relative", result.Error, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static CommandResult Run(string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = CliApplication.Run(args, output, error);
        return new CommandResult(exitCode, output.ToString(), error.ToString());
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"adr-guard-init-{Guid.NewGuid():N}");
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

    private sealed record CommandResult(int ExitCode, string Output, string Error);
}
