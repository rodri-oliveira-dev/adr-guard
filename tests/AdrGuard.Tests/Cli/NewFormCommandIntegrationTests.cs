using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AdrGuard.Cli;
using AdrGuard.Generation;
using AdrGuard.Processes;
using Xunit;

namespace AdrGuard.Tests.Cli;

public sealed class NewFormCommandIntegrationTests
{
    [Theory]
    [InlineData("minimal", "en-US", 3)]
    [InlineData("minimal", "pt-BR", 3)]
    [InlineData("extended", "en-US", 10)]
    [InlineData("extended", "pt-BR", 10)]
    public void DescribeReturnsVersionedBuiltInTemplateSchema(
        string template,
        string culture,
        int expectedFields)
    {
        var root = CreateDirectory();
        try
        {
            var result = Run(
                root,
                Request("describe"),
                "--template", template,
                "--culture", culture);

            Assert.Equal(ExitCodes.Success, result.Code);
            Assert.Equal(string.Empty, result.Error);
            using var response = JsonDocument.Parse(result.Output);
            Assert.Equal("1.0", response.RootElement.GetProperty("schemaVersion").GetString());
            Assert.Equal(template, response.RootElement.GetProperty("template").GetProperty("name").GetString());
            Assert.True(response.RootElement.GetProperty("template").GetProperty("compatible").GetBoolean());
            Assert.Equal(expectedFields, response.RootElement.GetProperty("template").GetProperty("fields").GetArrayLength());
            Assert.Empty(Directory.EnumerateFiles(root));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Theory]
    [InlineData("minimal", "en-US", 3)]
    [InlineData("minimal", "pt-BR", 3)]
    [InlineData("extended", "en-US", 6)]
    [InlineData("extended", "pt-BR", 6)]
    public void MadrDescribePreservesSchemaOneAndReturnsFormatSpecificFields(
        string template,
        string culture,
        int expectedFields)
    {
        var root = CreateDirectory();
        try
        {
            var result = Run(
                root,
                Request("describe"),
                "--adr-format", "madr-4",
                "--template", template,
                "--culture", culture);

            Assert.Equal(ExitCodes.Success, result.Code);
            using var response = JsonDocument.Parse(result.Output);
            Assert.Equal("1.0", response.RootElement.GetProperty("schemaVersion").GetString());
            var templateResponse = response.RootElement.GetProperty("template");
            Assert.Equal(template, templateResponse.GetProperty("name").GetString());
            Assert.Equal(expectedFields, templateResponse.GetProperty("fields").GetArrayLength());
            (string Id, bool Required)[] expectedFieldContracts = template == "minimal"
                ? [("context", true), ("optionsConsidered", true), ("decision", true)]
                :
                [
                    ("context", true),
                    ("decisionDrivers", false),
                    ("optionsConsidered", true),
                    ("decision", true),
                    ("consequences", false),
                    ("references", false),
                ];
            Assert.Equal(
                expectedFieldContracts,
                templateResponse.GetProperty("fields").EnumerateArray()
                    .Select(field => (
                        field.GetProperty("id").GetString()!,
                        field.GetProperty("required").GetBoolean()))
                    .ToArray());
            Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void MadrPreviewAndCreateUseSchemaOneRendererAndExplicitCheckAcceptsOutput()
    {
        var root = CreateDirectory();
        const string fields = """
            {
              "context": "Orders need durable asynchronous processing.",
              "decisionDrivers": "Durability and operability",
              "optionsConsidered": "* Keep synchronous processing\n* Adopt a managed queue",
              "decision": "Propose the managed queue because it satisfies the drivers.",
              "consequences": "Operations must monitor retries.",
              "references": "See the operations runbook."
            }
            """;
        try
        {
            var preview = Run(
                root,
                Request("preview", "Adopt a queue", fields),
                "--adr-format", "madr-4",
                "--template", "extended");
            Assert.Equal(ExitCodes.Success, preview.Code);
            Assert.Empty(Directory.EnumerateFileSystemEntries(root));
            using var previewJson = JsonDocument.Parse(preview.Output);
            Assert.Equal("1.0", previewJson.RootElement.GetProperty("schemaVersion").GetString());
            var markdown = previewJson.RootElement.GetProperty("markdown").GetString();
            Assert.Contains("### Consequences\n\nOperations must monitor retries.", markdown, StringComparison.Ordinal);
            Assert.Contains(
                "Propose the managed queue because it satisfies the drivers.",
                markdown,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "[EDIT",
                markdown,
                StringComparison.Ordinal);

            var create = Run(
                root,
                Request("create", "Adopt a queue", fields),
                "--adr-format", "madr-4",
                "--template", "extended");
            Assert.Equal(ExitCodes.Success, create.Code);
            using var createJson = JsonDocument.Parse(create.Output);
            var path = Assert.IsType<string>(createJson.RootElement.GetProperty("path").GetString());
            Assert.Equal(markdown, File.ReadAllText(path));
            Assert.True(createJson.RootElement.GetProperty("written").GetBoolean());
            Assert.False(createJson.RootElement.TryGetProperty("markdown", out _));

            using var checkOutput = new StringWriter();
            using var checkError = new StringWriter();
            var checkCode = CliApplication.Run(
                ["check", root, "--adr-format", "madr-4"],
                checkOutput,
                checkError,
                TestContext.Current.CancellationToken);
            Assert.Equal(ExitCodes.Success, checkCode);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void MadrExtendedFormOmitsEmptyOptionalSectionsAndRejectsUnversionedFieldIds()
    {
        var root = CreateDirectory();
        const string fields = """
            {
              "context": "Context.",
              "decisionDrivers": "",
              "optionsConsidered": "* Option A",
              "decision": "Propose option A.",
              "consequences": "",
              "references": ""
            }
            """;
        try
        {
            var preview = Run(
                root,
                Request("preview", "Choose A", fields),
                "--adr-format", "madr-4",
                "--template", "extended");
            Assert.Equal(ExitCodes.Success, preview.Code);
            using var response = JsonDocument.Parse(preview.Output);
            var markdown = response.RootElement.GetProperty("markdown").GetString();
            Assert.DoesNotContain("## Decision Drivers", markdown, StringComparison.Ordinal);
            Assert.DoesNotContain("### Consequences", markdown, StringComparison.Ordinal);
            Assert.DoesNotContain("## More Information", markdown, StringComparison.Ordinal);

            const string unversionedFields = """{"contextAndProblemStatement":"Context","consideredOptions":"* A","decisionOutcome":"Choose A"}""";
            var rejected = Run(
                root,
                Request("preview", "Choose A", unversionedFields),
                "--adr-format", "madr-4");
            Assert.Equal(ExitCodes.UsageError, rejected.Code);
            Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void PreviewAndCreateUseTheSameRendererWithoutGuidanceText()
    {
        var root = CreateDirectory();
        const string fields = """
            {
              "context": "O serviço precisa preservar **acentos** e [links](https://example.test).\n\n<img src=x onerror=alert(1)>\n\n- limite: 10 ms",
              "decision": "Usar uma fila `durável`.",
              "consequences": "Mais resiliência.\n\nCusto operacional maior."
            }
            """;
        try
        {
            var preview = Run(root, Request("preview", "Adotar fila", fields), "--culture", "pt-BR");
            Assert.Equal(ExitCodes.Success, preview.Code);
            Assert.Empty(Directory.EnumerateFiles(root));
            using var previewJson = JsonDocument.Parse(preview.Output);
            var previewMarkdown = previewJson.RootElement.GetProperty("markdown").GetString();
            Assert.Contains("**acentos**", previewMarkdown, StringComparison.Ordinal);
            Assert.Contains("<img src=x onerror=alert(1)>", previewMarkdown, StringComparison.Ordinal);
            Assert.DoesNotContain("[EDIT", previewMarkdown, StringComparison.Ordinal);
            Assert.DoesNotContain("Descreva o contexto", previewMarkdown, StringComparison.Ordinal);

            var create = Run(root, Request("create", "Adotar fila", fields), "--culture", "pt-BR");
            Assert.Equal(ExitCodes.Success, create.Code);
            using var createJson = JsonDocument.Parse(create.Output);
            var path = Assert.IsType<string>(createJson.RootElement.GetProperty("path").GetString());
            Assert.True(createJson.RootElement.GetProperty("written").GetBoolean());
            Assert.Equal(previewMarkdown, File.ReadAllText(path));
            Assert.False(createJson.RootElement.TryGetProperty("markdown", out _));
            Assert.InRange(Encoding.UTF8.GetByteCount(create.Output), 1, 1023);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void ExtendedTemplateIncludesOnlyOptionalSectionsWithContent()
    {
        var root = CreateDirectory();
        const string fields = """
            {
              "context": "Context content",
              "decisionDrivers": "Latency and operability",
              "optionsConsidered": "Keep current; adopt queue",
              "decision": "Adopt the queue",
              "rationale": "Best trade-off",
              "consequences": "A new operational dependency",
              "positiveConsequences": "Improved resilience",
              "negativeConsequences": "More operations",
              "risks": "Backlog growth",
              "references": ""
            }
            """;
        try
        {
            var result = Run(root, Request("preview", "Adopt queue", fields), "--template", "extended");
            Assert.Equal(ExitCodes.Success, result.Code);
            using var response = JsonDocument.Parse(result.Output);
            var markdown = response.RootElement.GetProperty("markdown").GetString();
            Assert.Contains("## Decision Drivers\n\nLatency and operability", markdown, StringComparison.Ordinal);
            Assert.Contains("## Risks\n\nBacklog growth", markdown, StringComparison.Ordinal);
            Assert.DoesNotContain("## References", markdown, StringComparison.Ordinal);
            Assert.Empty(Directory.EnumerateFiles(root));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void CustomTemplateIsReportedAsIncompatibleWithoutReadingOrReplacingIt()
    {
        var root = CreateDirectory();
        try
        {
            var missing = Path.Combine(root, "team-template.md");
            var result = Run(root, Request("describe"), "--template-file", missing);
            var madrResult = Run(
                root,
                Request("describe"),
                "--adr-format", "madr-4",
                "--template-file", missing);

            Assert.Equal(ExitCodes.Success, result.Code);
            using var response = JsonDocument.Parse(result.Output);
            var template = response.RootElement.GetProperty("template");
            Assert.Equal("custom", template.GetProperty("name").GetString());
            Assert.False(template.GetProperty("compatible").GetBoolean());
            Assert.Equal("custom-template-field-mapping-unavailable", template.GetProperty("reason").GetString());
            Assert.Equal(ExitCodes.Success, madrResult.Code);
            using var madrResponse = JsonDocument.Parse(madrResult.Output);
            Assert.False(madrResponse.RootElement.GetProperty("template").GetProperty("compatible").GetBoolean());
            Assert.Empty(Directory.EnumerateFiles(root));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Theory]
    [InlineData("{\"schemaVersion\":\"2.0\",\"operation\":\"describe\"}")]
    [InlineData("{\"schemaVersion\":\"1.0\",\"operation\":\"preview\",\"title\":\"Title\",\"fields\":{}}")]
    [InlineData("{\"schemaVersion\":\"1.0\",\"operation\":\"describe\",\"unexpected\":true}")]
    [InlineData("{\"schemaVersion\":\"1.0\",\"schemaVersion\":\"1.0\",\"operation\":\"describe\"}")]
    [InlineData("{\"schemaVersion\":\"1.0\",\"SchemaVersion\":\"2.0\",\"operation\":\"describe\"}")]
    [InlineData("{\"schemaVersion\":\"1.0\",\"Operation\":\"describe\"}")]
    [InlineData("{\"schemaVersion\":\"1.0\",\"operation\":\"describe\",\"title\":\"Unexpected\"}")]
    [InlineData("{\"schemaVersion\":\"1.0\",\"operation\":\"describe\",\"fields\":{}}")]
    public void InvalidRequestsFailClosedWithoutWriting(string request)
    {
        var root = CreateDirectory();
        try
        {
            var result = Run(root, request);
            Assert.Equal(ExitCodes.UsageError, result.Code);
            Assert.NotEmpty(result.Error);
            Assert.Empty(Directory.EnumerateFiles(root));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void CreationReallocatesAfterPreviewAndNeverOverwrites()
    {
        var root = CreateDirectory();
        const string fields = """{"context":"Context","decision":"Decision","consequences":"Consequences"}""";
        try
        {
            var preview = Run(root, Request("preview", "Target", fields));
            Assert.Equal(ExitCodes.Success, preview.Code);
            File.WriteAllText(
                Path.Combine(root, "0001-existing.md"),
                "# Existing\n\n## Status\n\nProposed\n\n## Context\n\nContext\n\n## Decision\n\nDecision\n\n## Consequences\n\nConsequences\n");

            var create = Run(root, Request("create", "Target", fields));
            Assert.Equal(ExitCodes.Success, create.Code);
            using var response = JsonDocument.Parse(create.Output);
            Assert.EndsWith("0002-target.md", response.RootElement.GetProperty("path").GetString(), StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(root, "0001-existing.md")));

            var collision = Run(root, Request("create", "Target", fields));
            Assert.Equal(ExitCodes.Success, collision.Code);
            using var collisionResponse = JsonDocument.Parse(collision.Output);
            Assert.EndsWith("0003-target.md", collisionResponse.RootElement.GetProperty("path").GetString(), StringComparison.Ordinal);
            Assert.Equal(3, Directory.GetFiles(root, "*.md").Length);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void FormInputBoundariesFailClosedWithoutWriting()
    {
        var root = CreateDirectory();
        const string validFields = """{"context":"Context","decision":"Decision","consequences":"Consequences"}""";
        try
        {
            var unsupportedField = """{"context":"Context","decision":"Decision","consequences":"Consequences","unknown":"value"}""";
            var missingRequiredField = """{"context":"Context","decision":"Decision"}""";
            var oversizedField = JsonSerializer.Serialize(new
            {
                context = new string('x', NewFormCommand.MaximumFieldCharacters + 1),
                decision = "Decision",
                consequences = "Consequences",
            });
            var oversizedMadrField = JsonSerializer.Serialize(new
            {
                context = new string(
                    'x',
                    AdrGenerationContextLimits.MaximumGeneratedFieldCharacters + 1),
                optionsConsidered = "* Option A",
                decision = "Propose option A.",
            });
            var unclosedMadrFence = JsonSerializer.Serialize(new
            {
                context = "```text\nunclosed",
                optionsConsidered = "* Option A",
                decision = "Propose option A.",
            });

            Assert.Equal(ExitCodes.UsageError, Run(root, Request("preview", "Title", unsupportedField)).Code);
            Assert.Equal(ExitCodes.UsageError, Run(root, Request("preview", "Title", missingRequiredField)).Code);
            Assert.Equal(ExitCodes.UsageError, Run(root, Request("preview", "Title", oversizedField)).Code);
            Assert.Equal(
                ExitCodes.UsageError,
                Run(
                    root,
                    Request("preview", "Title", oversizedMadrField),
                    "--adr-format", "madr-4").Code);
            var invalidMadrFence = Run(
                root,
                Request("preview", "Title", unclosedMadrFence),
                "--adr-format", "madr-4");
            Assert.Equal(ExitCodes.UsageError, invalidMadrFence.Code);
            Assert.Contains(
                "close every Markdown code fence",
                invalidMadrFence.Error,
                StringComparison.Ordinal);
            Assert.Equal(
                ExitCodes.UsageError,
                Run(root, Request("preview", "Title", """{"context":"Con\u0000text","decision":"Decision","consequences":"Consequences"}""")).Code);
            Assert.Equal(
                ExitCodes.UsageError,
                Run(root, Request("preview", "Title", """{"context":"   ","decision":"Decision","consequences":"Consequences"}""")).Code);
            Assert.Equal(ExitCodes.UsageError, Run(root, Request("preview", new string('x', 513), validFields)).Code);
            Assert.Equal(ExitCodes.UsageError, Run(root, Request("create", "Line one\nLine two", validFields)).Code);
            Assert.Equal(ExitCodes.UsageError, Run(root, Request("create", "!!!", validFields)).Code);
            Assert.Equal(
                ExitCodes.UsageError,
                Run(root, """{"schemaVersion":"1.0","operation":"preview","title":"Title","fields":{"context":{"a":{"b":{"c":{"d":{"e":{"f":{"g":{}}}}}}}}}}""").Code);
            Assert.Equal(
                ExitCodes.UsageError,
                Run(root, Request("preview", "Title", validFields), "--template-file", Path.Combine(root, "template.md")).Code);
            Assert.Equal(
                ExitCodes.UsageError,
                Run(root, Request("create", "Title", validFields), "--template-file", Path.Combine(root, "template.md")).Code);
            var invalidMadrCustom = Run(
                root,
                Request("preview", "Title", validFields),
                "--adr-format", "madr-4",
                "--template-file", Path.Combine(root, "template.md"));
            Assert.Equal(ExitCodes.UsageError, invalidMadrCustom.Code);
            Assert.Contains(
                "cannot be combined with --adr-format madr-4",
                invalidMadrCustom.Error,
                StringComparison.Ordinal);
            var invalidMadrCustomCreate = Run(
                root,
                Request("create", "Title", validFields),
                "--adr-format", "madr-4",
                "--template-file", Path.Combine(root, "template.md"));
            Assert.Equal(ExitCodes.UsageError, invalidMadrCustomCreate.Code);
            Assert.Contains(
                "cannot be combined with --adr-format madr-4",
                invalidMadrCustomCreate.Error,
                StringComparison.Ordinal);
            Assert.Equal(
                ExitCodes.OperationalError,
                Run(root, new string('x', NewFormCommand.MaximumInputCharacters + 1)).Code);
            Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void ProcessContractRejectsInvalidUtf8WithoutWriting()
    {
        var root = CreateDirectory();
        try
        {
            var startInfo = new ProcessStartInfo(ExecutablePathResolver.Resolve("dotnet"))
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(typeof(CliApplication).Assembly.Location);
            startInfo.ArgumentList.Add("new");
            startInfo.ArgumentList.Add(root);
            startInfo.ArgumentList.Add("--form");
            using var process = Process.Start(startInfo)!;
            process.StandardInput.BaseStream.Write([0xC3, 0x28]);
            process.StandardInput.Close();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.Equal(ExitCodes.UsageError, process.ExitCode);
            Assert.NotEmpty(error);
            Assert.Empty(Directory.EnumerateFiles(root));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static string Request(string operation, string? title = null, string? fields = null) =>
        title is null
            ? $$"""{"schemaVersion":"1.0","operation":"{{operation}}"}"""
            : $$"""{"schemaVersion":"1.0","operation":"{{operation}}","title":{{JsonSerializer.Serialize(title)}},"fields":{{fields}}}""";

    private static (int Code, string Output, string Error) Run(
        string directory,
        string input,
        params string[] options)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        using var reader = new StringReader(input);
        var arguments = new[] { "new", directory, "--form" }.Concat(options).ToArray();
        var code = CliApplication.Run(
            arguments,
            reader,
            output,
            error,
            TestContext.Current.CancellationToken);
        return (code, output.ToString(), error.ToString());
    }

    private static string CreateDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "adr-guard-form-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }
}
