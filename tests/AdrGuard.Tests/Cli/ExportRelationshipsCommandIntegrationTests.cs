using System.Text.Json;
using AdrGuard.Cli;
using AdrGuard.Parsing;
using AdrGuard.Validation;
using Xunit;

namespace AdrGuard.Tests.Cli;

public sealed class ExportRelationshipsCommandIntegrationTests
{
    [Fact]
    public void ExportsValidRelationshipsToDefaultRepositoryPath()
    {
        using var repository = new TemporaryRepository();
        repository.WriteAdr("0001-old.md", Adr("Old", "Superseded", "## Superseded by\n[New](0002-new.md)"));
        repository.WriteAdr("0002-new.md", Adr("New", "Accepted", "## Supersedes\n[Old](0001-old.md)"));

        var result = Run(repository, ["export", "relationships", repository.AdrDirectory]);

        Assert.Equal(ExitCodes.Success, result.Code);
        Assert.Equal(string.Empty, result.Error);
        var path = Path.Combine(repository.Root, ".adr-guard", "relationships.json");
        Assert.True(File.Exists(path));
        using var json = JsonDocument.Parse(File.ReadAllBytes(path));
        Assert.Equal("1.0", json.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal(2, json.RootElement.GetProperty("adrs").GetArrayLength());
        Assert.Equal(2, json.RootElement.GetProperty("relationships").GetArrayLength());
        Assert.All(
            json.RootElement.GetProperty("relationships").EnumerateArray(),
            relationship => Assert.Equal("resolved", relationship.GetProperty("resolution").GetString()));
        Assert.All(
            json.RootElement.GetProperty("adrs").EnumerateArray(),
            adr => Assert.StartsWith("docs/adr/", adr.GetProperty("path").GetString(), StringComparison.Ordinal));
        Assert.DoesNotContain(repository.Root, File.ReadAllText(path), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WritesArtifactAndReturnsSuccessWhenValidationHasIssues()
    {
        using var repository = new TemporaryRepository();
        repository.WriteAdr("0001-source.md", Adr("Source", "Approved", "## Depends on\n[Missing](0099-missing.md)"));

        var result = Run(repository, ["export", "relationships", repository.AdrDirectory]);

        Assert.Equal(ExitCodes.Success, result.Code);
        using var json = repository.ReadDefaultExport();
        Assert.NotEmpty(json.RootElement.GetProperty("issues").EnumerateArray());
        var relationship = Assert.Single(json.RootElement.GetProperty("relationships").EnumerateArray());
        Assert.Equal("unresolved", relationship.GetProperty("resolution").GetString());
        Assert.Equal(JsonValueKind.Null, relationship.GetProperty("targetPath").ValueKind);
    }

    [Fact]
    public void DuplicateIdDiagnosticsDoNotExposeTheAbsoluteRepositoryPath()
    {
        using var repository = new TemporaryRepository();
        repository.WriteAdr("0001-first.md", Adr("First", "Accepted"));
        repository.WriteAdr("0001-second.md", Adr("Second", "Accepted"));

        var result = Run(repository, ["export", "relationships", repository.AdrDirectory]);

        Assert.Equal(ExitCodes.Success, result.Code);
        var json = File.ReadAllText(repository.DefaultExportPath);
        Assert.DoesNotContain(repository.Root, json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("docs/adr/0001-first.md", json, StringComparison.Ordinal);
        Assert.Contains("docs/adr/0001-second.md", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportsAdrDirectoryWithoutRelationships()
    {
        using var repository = new TemporaryRepository();
        repository.WriteAdr("0001-only.md", Adr("Only", "Accepted"));

        Assert.Equal(
            ExitCodes.Success,
            Run(repository, ["export", "relationships", repository.AdrDirectory]).Code);

        using var json = repository.ReadDefaultExport();
        Assert.Single(json.RootElement.GetProperty("adrs").EnumerateArray());
        Assert.Empty(json.RootElement.GetProperty("relationships").EnumerateArray());
    }

    [Fact]
    public void ExportsEmptyDirectory()
    {
        using var repository = new TemporaryRepository();

        Assert.Equal(
            ExitCodes.Success,
            Run(repository, ["export", "relationships", repository.AdrDirectory]).Code);

        using var json = repository.ReadDefaultExport();
        Assert.Empty(json.RootElement.GetProperty("adrs").EnumerateArray());
        Assert.Empty(json.RootElement.GetProperty("relationships").EnumerateArray());
        Assert.Empty(json.RootElement.GetProperty("issues").EnumerateArray());
    }

    [Fact]
    public void CreatesCustomOutputDirectoryRelativeToRepositoryRoot()
    {
        using var repository = new TemporaryRepository();
        repository.WriteAdr("0001-only.md", Adr("Only", "Accepted"));

        var result = Run(
            repository,
            ["export", "relationships", repository.AdrDirectory, "--output", "artifacts/relationships.json"]);

        Assert.Equal(ExitCodes.Success, result.Code);
        Assert.True(File.Exists(Path.Combine(repository.Root, "artifacts", "relationships.json")));
        Assert.False(File.Exists(Path.Combine(repository.Root, ".adr-guard", "relationships.json")));
    }

    [Fact]
    public void SafelyReplacesExistingArtifact()
    {
        using var repository = new TemporaryRepository();
        repository.WriteAdr("0001-only.md", Adr("Only", "Accepted"));
        var path = repository.DefaultExportPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "old content");

        var result = Run(repository, ["export", "relationships", repository.AdrDirectory]);

        Assert.Equal(ExitCodes.Success, result.Code);
        Assert.NotEqual("old content", File.ReadAllText(path));
        using var json = JsonDocument.Parse(File.ReadAllBytes(path));
        Assert.Equal("1.0", json.RootElement.GetProperty("schemaVersion").GetString());
    }

    [Fact]
    public void WriteFailurePreservesExistingArtifactAndReturnsOperationalError()
    {
        using var repository = new TemporaryRepository();
        repository.WriteAdr("0001-only.md", Adr("Only", "Accepted"));
        Directory.CreateDirectory(Path.GetDirectoryName(repository.DefaultExportPath)!);
        File.WriteAllText(repository.DefaultExportPath, "existing");
        var writer = new AdrRelationshipsExportFileWriter(
            (temporaryPath, content) => File.WriteAllText(temporaryPath, content),
            (_, _) => throw new IOException("simulated promotion failure"));
        var arguments = new ExportRelationshipsCommandArguments(
            repository.AdrDirectory,
            null,
            AdrFormat.Canonical,
            null,
            false,
            null,
            null);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var code = ExportRelationshipsCommand.Run(
            arguments,
            output,
            error,
            TestContext.Current.CancellationToken,
            writer);

        Assert.Equal(ExitCodes.OperationalError, code);
        Assert.Equal("existing", File.ReadAllText(repository.DefaultExportPath));
        Assert.Contains("simulated promotion failure", error.ToString(), StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFiles(
            Path.GetDirectoryName(repository.DefaultExportPath)!,
            ".relationships.json.*.tmp"));
    }

    [Theory]
    [InlineData("../outside.json")]
    [InlineData("artifacts/relationships.txt")]
    public void RejectsUnsafeOrInvalidOutputPath(string outputPath)
    {
        using var repository = new TemporaryRepository();
        var outsidePath = Path.GetFullPath(outputPath, repository.Root);

        var result = Run(
            repository,
            ["export", "relationships", repository.AdrDirectory, "--output", outputPath]);

        Assert.Equal(ExitCodes.OperationalError, result.Code);
        Assert.False(File.Exists(outsidePath));
        Assert.Contains("Unable to export ADR relationships", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsOutputThroughSymbolicLinkWhenPlatformAllowsLinks()
    {
        using var repository = new TemporaryRepository();
        var outside = Directory.CreateTempSubdirectory("adr-guard-export-outside-");
        try
        {
            var link = Path.Combine(repository.Root, "linked");
            try
            {
                Directory.CreateSymbolicLink(link, outside.FullName);
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or PlatformNotSupportedException)
            {
                return;
            }

            var result = Run(
                repository,
                ["export", "relationships", repository.AdrDirectory, "--output", "linked/relationships.json"]);

            Assert.Equal(ExitCodes.OperationalError, result.Code);
            Assert.False(File.Exists(Path.Combine(outside.FullName, "relationships.json")));
        }
        finally
        {
            outside.Delete(recursive: true);
        }
    }

    [Fact]
    public void RejectsAbsoluteOutputPath()
    {
        using var repository = new TemporaryRepository();
        var outside = Path.Combine(Path.GetTempPath(), $"adr-guard-export-{Guid.NewGuid():N}.json");
        try
        {
            var result = Run(
                repository,
                ["export", "relationships", repository.AdrDirectory, "--output", outside]);

            Assert.Equal(ExitCodes.OperationalError, result.Code);
            Assert.False(File.Exists(outside));
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Theory]
    [InlineData(@"C:\temp\relationships.json")]
    [InlineData("C:/temp/relationships.json")]
    [InlineData(@"\\server\share\relationships.json")]
    public void RejectsWindowsStyleOutputPathOnUnix(string outputPath)
    {
        if (OperatingSystem.IsWindows()) return;

        using var repository = new TemporaryRepository();

        var result = Run(
            repository,
            ["export", "relationships", repository.AdrDirectory, "--output", outputPath]);

        Assert.Equal(ExitCodes.OperationalError, result.Code);
        Assert.False(File.Exists(Path.Combine(repository.Root, outputPath)));
        Assert.Contains("portable relative path", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void SkipsSymbolicLinkedInputWithoutReadingOutsideRepository()
    {
        using var repository = new TemporaryRepository();
        var outside = Path.Combine(Path.GetTempPath(), $"adr-guard-export-outside-{Guid.NewGuid():N}.md");
        File.WriteAllText(outside, Adr("Outside", "Accepted"));
        var link = Path.Combine(repository.AdrDirectory, "0001-linked.md");
        try
        {
            try
            {
                File.CreateSymbolicLink(link, outside);
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or PlatformNotSupportedException)
            {
                return;
            }

            var result = Run(repository, ["export", "relationships", repository.AdrDirectory]);

            Assert.Equal(ExitCodes.Success, result.Code);
            using var json = repository.ReadDefaultExport();
            Assert.Empty(json.RootElement.GetProperty("adrs").EnumerateArray());
            Assert.DoesNotContain("Outside", File.ReadAllText(repository.DefaultExportPath), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public void RejectsMalformedUtf8WithoutWritingArtifact()
    {
        using var repository = new TemporaryRepository();
        File.WriteAllBytes(
            Path.Combine(repository.AdrDirectory, "0001-invalid.md"),
            [0x23, 0x20, 0xC3, 0x28]);

        var result = Run(repository, ["export", "relationships", repository.AdrDirectory]);

        Assert.Equal(ExitCodes.OperationalError, result.Code);
        Assert.False(File.Exists(repository.DefaultExportPath));
        Assert.Contains("Unable to export ADR relationships", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsOversizedAdrWithoutWritingArtifact()
    {
        using var repository = new TemporaryRepository();
        File.WriteAllBytes(
            Path.Combine(repository.AdrDirectory, "0001-large.md"),
            new byte[AdrDocumentLoader.MaximumBoundedAdrBytes + 1]);

        var result = Run(repository, ["export", "relationships", repository.AdrDirectory]);

        Assert.Equal(ExitCodes.OperationalError, result.Code);
        Assert.False(File.Exists(repository.DefaultExportPath));
        Assert.Contains("exceeds", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void ProducesByteIdenticalOutputAcrossExecutions()
    {
        using var repository = new TemporaryRepository();
        repository.WriteAdr("0002-second.md", Adr("Second", "Accepted"));
        repository.WriteAdr("0001-first.md", Adr("First", "Proposed", "## Depends on\n[Second](0002-second.md)"));

        Assert.Equal(ExitCodes.Success, Run(repository, ["export", "relationships", repository.AdrDirectory]).Code);
        var first = File.ReadAllBytes(repository.DefaultExportPath);
        Assert.Equal(ExitCodes.Success, Run(repository, ["export", "relationships", repository.AdrDirectory]).Code);
        var second = File.ReadAllBytes(repository.DefaultExportPath);

        Assert.Equal(first, second);
        Assert.DoesNotContain("generatedAt", System.Text.Encoding.UTF8.GetString(second), StringComparison.Ordinal);
    }

    [Fact]
    public void UsesUsageAndOperationalExitCodesForTheirRespectiveFailures()
    {
        using var repository = new TemporaryRepository();

        Assert.Equal(
            ExitCodes.UsageError,
            Run(repository, ["export", "relationships", "--output"]).Code);
        Assert.Equal(
            ExitCodes.OperationalError,
            Run(repository, ["export", "relationships", Path.Combine(repository.Root, "missing")]).Code);
    }

    [Fact]
    public void UsesConfiguredDirectoryAndValidationOptions()
    {
        using var repository = new TemporaryRepository();
        repository.WriteAdr("decision.md", Adr("Decision", "Under Review"));
        File.WriteAllText(
            Path.Combine(repository.Root, ".adrguard.yml"),
            """
            schema-version: 1
            adr-directory: docs/adr
            filename-policy: unnumbered
            lifecycle-statuses: Under Review=proposed
            """);

        var result = Run(repository, ["export", "relationships"]);

        Assert.Equal(ExitCodes.Success, result.Code);
        using var json = repository.ReadDefaultExport();
        var adr = Assert.Single(json.RootElement.GetProperty("adrs").EnumerateArray());
        Assert.Equal("slug:decision", adr.GetProperty("identifier").GetString());
        Assert.Equal("Proposed", adr.GetProperty("status").GetString());
    }

    private static CommandResult Run(TemporaryRepository repository, IReadOnlyList<string> arguments)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = CliApplication.Run(
            arguments,
            output,
            error,
            invocationDirectory: repository.Root);
        return new CommandResult(code, output.ToString(), error.ToString());
    }

    private static string Adr(string title, string status, string additional = "") =>
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
        {additional}
        """;

    private sealed record CommandResult(int Code, string Output, string Error);

    private sealed class TemporaryRepository : IDisposable
    {
        private readonly string ownedRoot;

        internal TemporaryRepository()
        {
            ownedRoot = Path.Combine(Path.GetTempPath(), $"adr-guard-cli-export-{Guid.NewGuid():N}");
            Root = Path.Combine(ownedRoot, "repository");
            AdrDirectory = Path.Combine(Root, "docs", "adr");
            Directory.CreateDirectory(Path.Combine(Root, ".git"));
            Directory.CreateDirectory(AdrDirectory);
        }

        internal string Root { get; }
        internal string AdrDirectory { get; }
        internal string DefaultExportPath => Path.Combine(Root, ".adr-guard", "relationships.json");

        internal void WriteAdr(string fileName, string content) =>
            File.WriteAllText(Path.Combine(AdrDirectory, fileName), content);

        internal JsonDocument ReadDefaultExport() =>
            JsonDocument.Parse(File.ReadAllBytes(DefaultExportPath));

        public void Dispose()
        {
            if (Directory.Exists(ownedRoot)) Directory.Delete(ownedRoot, recursive: true);
        }
    }
}
