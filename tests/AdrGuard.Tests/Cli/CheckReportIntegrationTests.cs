using AdrGuard.Cli;
using System.Text.Json;
using Xunit;

namespace AdrGuard.Tests.Cli;

public sealed class CheckReportIntegrationTests
{
    [Theory]
    [InlineData("json")]
    [InlineData("sarif")]
    public void MachineReportIsValidJsonOnValidationFailure(string format)
    {
        var root = CreateTempDirectory();

        try
        {
            File.WriteAllText(Path.Combine(root, "0001-invalid.md"), "# Invalid");
            var result = Run(["check", root, "--format", format]);

            Assert.Equal(ExitCodes.ValidationFailed, result.ExitCode);
            Assert.Equal(string.Empty, result.Error);
            using var document = JsonDocument.Parse(result.Output);

            if (format == "json")
            {
                Assert.Equal("1.0", document.RootElement.GetProperty("schemaVersion").GetString());
                Assert.False(document.RootElement.GetProperty("valid").GetBoolean());
                Assert.True(document.RootElement.GetProperty("diagnostics").GetArrayLength() > 0);
            }
            else
            {
                Assert.Equal("2.1.0", document.RootElement.GetProperty("version").GetString());
                var firstResult = document.RootElement
                    .GetProperty("runs")[0]
                    .GetProperty("results")[0];
                Assert.StartsWith("ADR", firstResult.GetProperty("ruleId").GetString(), StringComparison.Ordinal);
                var physicalLocation = firstResult.GetProperty("locations")[0]
                    .GetProperty("physicalLocation");
                Assert.False(physicalLocation.TryGetProperty("region", out _));
            }
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Theory]
    [InlineData("json")]
    [InlineData("sarif")]
    public void MachineReportIncludesKnownSourceLocation(string format)
    {
        var root = CreateTempDirectory();

        try
        {
            File.WriteAllText(Path.Combine(root, "0001-invalid.md"), """
                # Invalid

                ## Status
                Unknown

                ## Context
                Context.

                ## Decision
                Decision.

                ## Consequences
                Consequences.
                """);
            var compatible = Run(["check", root, "--format", format]);
            using (var compatibleDocument = JsonDocument.Parse(compatible.Output))
            {
                var compatibleDiagnostic = format == "json"
                    ? compatibleDocument.RootElement.GetProperty("diagnostics").EnumerateArray()
                        .Single(item => item.GetProperty("code").GetString() == "ADR004")
                    : compatibleDocument.RootElement.GetProperty("runs")[0]
                        .GetProperty("results").EnumerateArray()
                        .Single(item => item.GetProperty("ruleId").GetString() == "ADR004")
                        .GetProperty("locations")[0]
                        .GetProperty("physicalLocation");
                Assert.False(compatibleDiagnostic.TryGetProperty(format == "json" ? "line" : "region", out _));
            }

            var result = Run(["check", root, "--format", format, "--diagnostic-locations"]);

            Assert.Equal(ExitCodes.ValidationFailed, result.ExitCode);
            using var document = JsonDocument.Parse(result.Output);
            var diagnostic = format == "json"
                ? document.RootElement.GetProperty("diagnostics").EnumerateArray()
                    .Single(item => item.GetProperty("code").GetString() == "ADR004")
                : document.RootElement.GetProperty("runs")[0]
                    .GetProperty("results").EnumerateArray()
                    .Single(item => item.GetProperty("ruleId").GetString() == "ADR004")
                    .GetProperty("locations")[0]
                    .GetProperty("physicalLocation")
                    .GetProperty("region");

            Assert.Equal(3, diagnostic.GetProperty(format == "json" ? "line" : "startLine").GetInt32());
            Assert.Equal(1, diagnostic.GetProperty(format == "json" ? "column" : "startColumn").GetInt32());
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Theory]
    [InlineData("json")]
    [InlineData("sarif")]
    public void MachineReportIsDeterministicAndValidOnSuccess(string format)
    {
        var root = CreateTempDirectory();

        try
        {
            File.WriteAllText(Path.Combine(root, "0001-valid.md"), ValidMarkdown);

            var first = Run(["check", root, "--format", format]);
            var second = Run(["check", root, "--format", format]);

            Assert.Equal(ExitCodes.Success, first.ExitCode);
            Assert.Equal(first.Output, second.Output);
            Assert.Equal(string.Empty, first.Error);
            using var document = JsonDocument.Parse(first.Output);
            Assert.NotEqual(JsonValueKind.Undefined, document.RootElement.ValueKind);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void TextRemainsDefaultAndInvalidFormatIsUsageError()
    {
        var root = CreateTempDirectory();

        try
        {
            File.WriteAllText(Path.Combine(root, "0001-valid.md"), ValidMarkdown);

            var legacy = Run(["check", root]);
            Assert.Equal(ExitCodes.Success, legacy.ExitCode);
            Assert.StartsWith("Validated 1 ADR(s)", legacy.Output, StringComparison.Ordinal);

            var invalid = Run(["check", root, "--format", "xml"]);
            Assert.Equal(ExitCodes.UsageError, invalid.ExitCode);
            Assert.Equal(string.Empty, invalid.Output);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private const string ValidMarkdown = """
        # Valid

        ## Status
        Accepted

        ## Context
        Context.

        ## Decision
        Decision.

        ## Consequences
        Consequences.
        """;

    private static CommandResult Run(string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = CliApplication.Run(args, output, error);
        return new CommandResult(exitCode, output.ToString(), error.ToString());
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"adr-guard-check-report-{Guid.NewGuid():N}");
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
