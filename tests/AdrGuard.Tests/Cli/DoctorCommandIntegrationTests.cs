using AdrGuard.Cli;
using Xunit;

namespace AdrGuard.Tests.Cli;

public sealed class DoctorCommandIntegrationTests
{
    [Fact]
    public void ConfiguredRepositoryProducesOrderedSuccessfulReport()
    {
        var root = CreateRepository(gitMarkerAsFile: false, createAdrDirectory: true);

        try
        {
            var result = RunDoctor(root);

            Assert.Equal(ExitCodes.Success, result.ExitCode);
            Assert.Equal(string.Empty, result.Error);
            Assert.Contains("Summary: 6 OK, 0 WARNING, 0 ERROR", result.Output, StringComparison.Ordinal);
            AssertOrdered(
                result.Output,
                "OK CLI:",
                "OK Repository:",
                "OK Configuration:",
                "OK ADR directory:",
                "OK .NET runtime:",
                "OK Git:",
                "Summary:");
            Assert.DoesNotContain(root, result.Output, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void MissingConfiguredAdrDirectoryIsAnOperationalDiagnostic()
    {
        var root = CreateRepository(gitMarkerAsFile: false, createAdrDirectory: false);

        try
        {
            var result = RunDoctor(root);

            Assert.Equal(ExitCodes.OperationalError, result.ExitCode);
            Assert.Contains("ERROR ADR directory: docs/adr does not exist.", result.Output, StringComparison.Ordinal);
            Assert.Contains("Summary: 5 OK, 0 WARNING, 1 ERROR", result.Output, StringComparison.Ordinal);
            Assert.Equal(string.Empty, result.Error);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void InvalidConfigurationIsReportedWithoutLoadingAdrDirectory()
    {
        var root = CreateRepository(gitMarkerAsFile: false, createAdrDirectory: true);
        File.WriteAllText(
            Path.Combine(root, ".adrguard.yml"),
            "schema-version: 1\nadrs: docs/adr\n");

        try
        {
            var result = RunDoctor(root);

            Assert.Equal(ExitCodes.OperationalError, result.ExitCode);
            Assert.Contains(
                "ERROR Configuration: .adrguard.yml line 2 has unknown property 'adrs'.",
                result.Output,
                StringComparison.Ordinal);
            Assert.Contains(
                "WARNING ADR directory: not checked because configuration is invalid.",
                result.Output,
                StringComparison.Ordinal);
            Assert.DoesNotContain(root, result.Output, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(string.Empty, result.Error);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void DirectoryOutsideRepositoryProducesCompletedErrorReport()
    {
        var root = CreateDirectoryOutsideRepository();

        try
        {
            var result = RunDoctor(root);

            Assert.Equal(ExitCodes.OperationalError, result.ExitCode);
            Assert.Contains(
                "ERROR Repository: no ADR Guard or Git repository was detected.",
                result.Output,
                StringComparison.Ordinal);
            Assert.Contains(
                "WARNING Configuration: not checked because no repository root was identified.",
                result.Output,
                StringComparison.Ordinal);
            Assert.Contains(
                "WARNING ADR directory: not checked because no repository root was identified.",
                result.Output,
                StringComparison.Ordinal);
            Assert.Equal(string.Empty, result.Error);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void ConfigurationIsReadFromSelectedDirectoryInsteadOfAncestor()
    {
        var root = CreateRepository(gitMarkerAsFile: false, createAdrDirectory: true);
        var selectedDirectory = Directory.CreateDirectory(Path.Combine(root, "src")).FullName;

        try
        {
            var result = RunDoctor(selectedDirectory);

            Assert.Equal(ExitCodes.Success, result.ExitCode);
            Assert.Contains(
                "WARNING Configuration: .adrguard.yml was not found; legacy defaults remain active.",
                result.Output,
                StringComparison.Ordinal);
            Assert.Contains(
                "WARNING ADR directory: the legacy scope is the selected root; no dedicated ADR directory is configured.",
                result.Output,
                StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void NestedConfigurationRecognizesAncestorGitMarker()
    {
        var root = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        var selectedDirectory = Directory.CreateDirectory(Path.Combine(root, "component")).FullName;
        Directory.CreateDirectory(Path.Combine(selectedDirectory, "docs", "adr"));
        File.WriteAllText(
            Path.Combine(selectedDirectory, ".adrguard.yml"),
            "schema-version: 1\nadr-directory: docs/adr\n");

        try
        {
            var result = RunDoctor(selectedDirectory);

            Assert.Equal(ExitCodes.Success, result.ExitCode);
            Assert.Contains(
                "OK Repository: detected with a Git worktree marker.",
                result.Output,
                StringComparison.Ordinal);
            Assert.Contains("Summary: 6 OK, 0 WARNING, 0 ERROR", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void InvalidEncodingInPathContainingSizeTextIsNotReportedAsOversized()
    {
        var parent = CreateTempDirectory();
        var root = Directory.CreateDirectory(Path.Combine(parent, "exceeds the boundary")).FullName;
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        File.WriteAllBytes(Path.Combine(root, ".adrguard.yml"), [0xc3, 0x28]);

        try
        {
            var result = RunDoctor(root);

            Assert.Equal(ExitCodes.OperationalError, result.ExitCode);
            Assert.Contains(
                "ERROR Configuration: .adrguard.yml could not be read or parsed.",
                result.Output,
                StringComparison.Ordinal);
            Assert.DoesNotContain("exceeds the 65536-byte limit", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(parent);
        }
    }

    [Fact]
    public void ExplicitRepositoryDoesNotRequireInvocationDirectory()
    {
        var root = CreateRepository(gitMarkerAsFile: false, createAdrDirectory: true);

        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = DoctorCommand.Run(
                ["doctor", root],
                "1.9.0+tests",
                invocationDirectory: null,
                output,
                error,
                CancellationToken.None,
                (_, _) => "git version 2.47.1");

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void UnsafePathAndHostilePropertyAreSanitized()
    {
        var root = CreateRepository(gitMarkerAsFile: false, createAdrDirectory: true);
        const string SecretLikeText = "TOP_SECRET_TOKEN_123456789";
        File.WriteAllText(
            Path.Combine(root, ".adrguard.yml"),
            $"schema-version: 1\n{SecretLikeText}: ../outside\n");

        try
        {
            var result = RunDoctor(root);

            Assert.Equal(ExitCodes.OperationalError, result.ExitCode);
            Assert.Contains("contains an unknown property", result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain(SecretLikeText, result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain(root, result.Output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("../outside", result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("::", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void DoctorIsReadOnlyAndWarningsStillReturnSuccess()
    {
        var root = CreateRepository(gitMarkerAsFile: null, createAdrDirectory: true);
        var configurationPath = Path.Combine(root, ".adrguard.yml");
        var beforeFiles = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
        var beforeConfiguration = File.ReadAllBytes(configurationPath);
        var beforeWriteTime = File.GetLastWriteTimeUtc(configurationPath);

        try
        {
            var result = RunDoctor(
                root,
                (_, _) => throw new InvalidOperationException("simulated unavailable Git"));

            Assert.Equal(ExitCodes.Success, result.ExitCode);
            Assert.Contains("WARNING Repository:", result.Output, StringComparison.Ordinal);
            Assert.Contains("WARNING Git:", result.Output, StringComparison.Ordinal);
            Assert.Contains("Summary: 4 OK, 2 WARNING, 0 ERROR", result.Output, StringComparison.Ordinal);
            Assert.Equal(beforeFiles, Directory.GetFiles(root, "*", SearchOption.AllDirectories));
            Assert.Equal(beforeConfiguration, File.ReadAllBytes(configurationPath));
            Assert.Equal(beforeWriteTime, File.GetLastWriteTimeUtc(configurationPath));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void GitWorktreeFileIsRecognizedWithoutReadingItsContents()
    {
        var root = CreateRepository(gitMarkerAsFile: true, createAdrDirectory: true);

        try
        {
            var result = RunDoctor(root);

            Assert.Equal(ExitCodes.Success, result.ExitCode);
            Assert.Contains("OK Repository: detected with a Git worktree marker.", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void GitVersionOutputIsBoundedToARecognizedSafeToken()
    {
        var root = CreateRepository(gitMarkerAsFile: false, createAdrDirectory: true);

        try
        {
            var appleResult = RunDoctor(root, (_, _) => "git version 2.39.5 (Apple Git-154)");
            var hostileResult = RunDoctor(root, (_, _) => "git version TOP_SECRET_TOKEN_123456789\n::warning::injected");

            Assert.Equal(ExitCodes.Success, appleResult.ExitCode);
            Assert.Contains("OK Git: 2.39.5 available.", appleResult.Output, StringComparison.Ordinal);
            Assert.Equal(ExitCodes.Success, hostileResult.ExitCode);
            Assert.Contains("WARNING Git: returned an unrecognized version.", hostileResult.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("TOP_SECRET_TOKEN", hostileResult.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("::warning::", hostileResult.Output, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void HelpBypassesInvalidConfigurationAndGlobalHelpListsDoctor()
    {
        var root = CreateTempDirectory();
        File.WriteAllText(Path.Combine(root, ".adrguard.yml"), "not-valid");

        try
        {
            using var globalOutput = new StringWriter();
            using var globalError = new StringWriter();
            var globalExit = CliApplication.Run(["--help"], globalOutput, globalError);

            using var commandOutput = new StringWriter();
            using var commandError = new StringWriter();
            var commandExit = CliApplication.Run(
                ["doctor", "--help"],
                commandOutput,
                commandError,
                invocationDirectory: root);

            Assert.Equal(ExitCodes.Success, globalExit);
            Assert.Contains("adr-guard doctor [repository]", globalOutput.ToString(), StringComparison.Ordinal);
            Assert.Equal(string.Empty, globalError.ToString());
            Assert.Equal(ExitCodes.Success, commandExit);
            Assert.Contains("Diagnose the selected local repository", commandOutput.ToString(), StringComparison.Ordinal);
            Assert.Equal(string.Empty, commandError.ToString());
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Theory]
    [InlineData("--unknown")]
    [InlineData("--help", ".")]
    [InlineData(".", "extra")]
    public void InvalidArgumentsUseTheCommandUsageContract(params string[] arguments)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = CliApplication.Run(["doctor", .. arguments], output, error);

        Assert.Equal(ExitCodes.UsageError, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Equal(
            "Invalid arguments for 'doctor'." + Environment.NewLine
            + "Run 'adr-guard doctor --help' for usage." + Environment.NewLine,
            error.ToString());
    }

    private static DoctorResult RunDoctor(
        string root,
        Func<string, CancellationToken, string>? gitVersionProbe = null)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = DoctorCommand.Run(
            ["doctor", root],
            "1.9.0+tests",
            root,
            output,
            error,
            CancellationToken.None,
            gitVersionProbe ?? ((_, _) => "git version 2.47.1.windows.1"));
        return new DoctorResult(exitCode, output.ToString(), error.ToString());
    }

    private static string CreateRepository(bool? gitMarkerAsFile, bool createAdrDirectory)
    {
        var root = CreateTempDirectory();
        if (gitMarkerAsFile == true)
        {
            File.WriteAllText(Path.Combine(root, ".git"), "untrusted worktree marker");
        }
        else if (gitMarkerAsFile == false)
        {
            Directory.CreateDirectory(Path.Combine(root, ".git"));
        }

        File.WriteAllText(
            Path.Combine(root, ".adrguard.yml"),
            "schema-version: 1\nadr-directory: docs/adr\n");
        if (createAdrDirectory)
        {
            Directory.CreateDirectory(Path.Combine(root, "docs", "adr"));
        }

        return root;
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"adr-guard-doctor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateDirectoryOutsideRepository()
    {
        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        for (var current = new DirectoryInfo(tempRoot);
             current is not null;
             current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, ".adrguard.yml"))
                || Directory.Exists(Path.Combine(current.FullName, ".git"))
                || File.Exists(Path.Combine(current.FullName, ".git")))
            {
                throw new InvalidOperationException(
                    "The system temporary directory must be outside a repository for this test.");
            }
        }

        return CreateTempDirectory();
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void AssertOrdered(string text, params string[] values)
    {
        var previousIndex = -1;
        foreach (var value in values)
        {
            var index = text.IndexOf(value, StringComparison.Ordinal);
            Assert.True(index > previousIndex, $"Expected '{value}' after index {previousIndex}. Report: {text}");
            previousIndex = index;
        }
    }

    private sealed record DoctorResult(int ExitCode, string Output, string Error);
}
