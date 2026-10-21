using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AdrGuard.Cli;
using AdrGuard.Processes;
using AdrGuard.Validation;
using Xunit;

namespace AdrGuard.Tests.Cli;

public sealed class EditFormCommandIntegrationTests
{
    [Fact]
    public void LoadAndSaveCanonicalPreserveIdentityAndUnknownMarkdown()
    {
        var root = CreateDirectory();
        var path = Path.Combine(root, "0007-use-cache.md");
        const string original = """
            <!-- owner: architecture -->
            # Use cache

            ## Status

            Proposed

            ## Context

            Old context.

            ## Decision

            Old decision.

            ### Implementation note

            Preserve this nested content.

            ## Consequences

            Old consequences.

            ## Team Extension

            Preserve this unknown section.
            """;
        try
        {
            File.WriteAllText(path, original);
            var load = Run(path, LoadRequest(), "canonical");
            Assert.Equal(ExitCodes.Success, load.Code);
            var request = SaveRequest(load.Output, title: "Use distributed cache", values =>
            {
                values[FieldId(load.Output, "Context")] = "New context.";
                values[FieldId(load.Output, "Status")] = "Accepted";
            });

            var save = Run(path, request, "canonical");
            Assert.Equal(ExitCodes.Success, save.Code);
            Assert.True(JsonNode.Parse(save.Output)!["written"]!.GetValue<bool>());
            Assert.True(File.Exists(path));
            Assert.Equal("0007-use-cache.md", Path.GetFileName(path));
            var updated = File.ReadAllText(path);
            Assert.Contains("# Use distributed cache", updated, StringComparison.Ordinal);
            Assert.Contains("## Status\n\nAccepted", updated, StringComparison.Ordinal);
            Assert.Contains("## Context\n\nNew context.", updated, StringComparison.Ordinal);
            Assert.Contains("### Implementation note\n\nPreserve this nested content.", updated, StringComparison.Ordinal);
            Assert.Contains("<!-- owner: architecture -->", updated, StringComparison.Ordinal);
            Assert.Contains("## Team Extension\n\nPreserve this unknown section.", updated, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void MadrEditPreservesUnknownMetadataAndSections()
    {
        var root = CreateDirectory();
        var path = Path.Combine(root, "0001-storage.md");
        const string original = """
            ---
            status: "proposed"
            date: 2026-10-12
            x-team: platform
            ---

            # Use storage

            ## Context and Problem Statement

            Context.

            ## Considered Options

            A and B.

            ## Decision Outcome

            Choose A.

            ## More Information

            Keep this.
            """;
        try
        {
            File.WriteAllText(path, original);
            var load = Run(path, LoadRequest(), "madr-4");
            Assert.Equal(ExitCodes.Success, load.Code);
            var request = SaveRequest(load.Output, "Use durable storage", values =>
                values[FieldId(load.Output, "Decision Outcome")] = "Choose B.");
            var save = Run(path, request, "madr-4");
            Assert.Equal(ExitCodes.Success, save.Code);
            var updated = File.ReadAllText(path);
            Assert.Contains("x-team: platform", updated, StringComparison.Ordinal);
            Assert.Contains("## More Information\n\nKeep this.", updated, StringComparison.Ordinal);
            Assert.Contains("## Decision Outcome\n\nChoose B.", updated, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void SaveRejectsExternalChangesAndInvalidRequiredFields()
    {
        var root = CreateDirectory();
        var path = Path.Combine(root, "0001-choice.md");
        try
        {
            File.WriteAllText(path, Canonical);
            var load = Run(path, LoadRequest(), "canonical");
            Assert.Equal(ExitCodes.Success, load.Code);
            var invalid = SaveRequest(load.Output, "Choice", values => values[FieldId(load.Output, "Decision")] = string.Empty);
            Assert.Equal(ExitCodes.UsageError, Run(path, invalid, "canonical").Code);
            Assert.Equal(Canonical, File.ReadAllText(path));

            var valid = SaveRequest(load.Output, "Updated choice", _ => { });
            File.AppendAllText(path, "\nExternal change.\n");
            var conflict = Run(path, valid, "canonical");
            Assert.Equal(ExitCodes.OperationalError, conflict.Code);
            Assert.Contains("changed after it was loaded", conflict.Error, StringComparison.Ordinal);
            Assert.Contains("External change.", File.ReadAllText(path), StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void SavePopulatesAnEmptyOptionalSectionWithoutDamagingTheNextHeading()
    {
        var root = CreateDirectory();
        var path = Path.Combine(root, "0002-empty-section.md");
        var original = Canonical.Replace(
            "## Decision\n\nDecision.",
            "## Options\n\n## Decision\n\nDecision.",
            StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);
        try
        {
            File.WriteAllText(path, original);
            var load = Run(path, LoadRequest(), "canonical");
            Assert.Equal(ExitCodes.Success, load.Code);
            var request = SaveRequest(load.Output, "Choice", values =>
                values[FieldId(load.Output, "Options")] = "Option A\nOption B");

            var save = Run(path, request, "canonical");
            Assert.Equal(ExitCodes.Success, save.Code);
            var updated = File.ReadAllText(path);
            Assert.Contains("## Options\r\n\r\nOption A\r\nOption B\r\n\r\n## Decision", updated, StringComparison.Ordinal);
            Assert.DoesNotContain("Option B## Decision", updated, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void SaveRejectsANewDependencyOnAnInactiveNeighbor()
    {
        var root = CreateDirectory();
        var inactivePath = Path.Combine(root, "0001-old.md");
        var path = Path.Combine(root, "0002-current.md");
        var current = Canonical.Replace(
            "## Decision\n\nDecision.",
            "## Dependencies\n\n## Decision\n\nDecision.",
            StringComparison.Ordinal);
        try
        {
            File.WriteAllText(inactivePath, Canonical.Replace("Proposed", "Deprecated", StringComparison.Ordinal));
            File.WriteAllText(path, current.Replace("Proposed", "Accepted", StringComparison.Ordinal));
            var load = Run(path, LoadRequest(), "canonical");
            Assert.Equal(ExitCodes.Success, load.Code);
            var request = SaveRequest(load.Output, "Choice", values =>
                values[FieldId(load.Output, "Dependencies")] = "[Old](0001-old.md)");

            var save = Run(path, request, "canonical");

            Assert.Equal(ExitCodes.ValidationFailed, save.Code);
            Assert.Contains(ValidationCodes.InactiveDependency, save.Error, StringComparison.Ordinal);
            Assert.Equal(current.Replace("Proposed", "Accepted", StringComparison.Ordinal), File.ReadAllText(path));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void SaveRejectsAnUpdateThatInvalidatesAParentDirectoryDependency()
    {
        var root = CreateDirectory();
        var nested = Path.Combine(root, "team");
        var parentPath = Path.Combine(root, "0001-parent.md");
        var childPath = Path.Combine(nested, "0002-child.md");
        var parent = Canonical
            .Replace("# Choice", "# Parent", StringComparison.Ordinal)
            .Replace("Proposed", "Accepted", StringComparison.Ordinal)
            .Replace(
                "## Decision\n\nDecision.",
                "## Dependencies\n\n[Child](team/0002-child.md)\n\n## Decision\n\nDecision.",
                StringComparison.Ordinal);
        var child = Canonical
            .Replace("# Choice", "# Child", StringComparison.Ordinal)
            .Replace("Proposed", "Accepted", StringComparison.Ordinal);
        try
        {
            Directory.CreateDirectory(nested);
            File.WriteAllText(parentPath, parent);
            File.WriteAllText(childPath, child);
            var load = Run(childPath, LoadRequest(), "canonical", root);
            Assert.Equal(ExitCodes.Success, load.Code);
            var request = SaveRequest(load.Output, "Child", values =>
                values[FieldId(load.Output, "Status")] = "Deprecated");

            var save = Run(childPath, request, "canonical", root);

            Assert.Equal(ExitCodes.ValidationFailed, save.Code);
            Assert.Contains(ValidationCodes.InactiveDependency, save.Error, StringComparison.Ordinal);
            Assert.Equal(child, File.ReadAllText(childPath));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void SaveAcceptsWindowsTargetPathWithDifferentCasing()
    {
        if (!OperatingSystem.IsWindows()) return;

        var root = CreateDirectory();
        var path = Path.Combine(root, "0003-case.md");
        try
        {
            File.WriteAllText(path, Canonical);
            var differentlyCasedPath = path.ToUpperInvariant();
            var load = Run(differentlyCasedPath, LoadRequest(), "canonical", root);
            Assert.Equal(ExitCodes.Success, load.Code);

            var save = Run(
                differentlyCasedPath,
                SaveRequest(load.Output, "Updated choice", _ => { }),
                "canonical",
                root);

            Assert.Equal(ExitCodes.Success, save.Code);
            Assert.Contains("# Updated choice", File.ReadAllText(path), StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void EditRejectsATargetOutsideTheExplicitAdrDirectory()
    {
        var root = CreateDirectory();
        var adrDirectory = Path.Combine(root, "adrs");
        var outsidePath = Path.Combine(root, "outside.md");
        try
        {
            Directory.CreateDirectory(adrDirectory);
            File.WriteAllText(outsidePath, Canonical);

            var load = Run(outsidePath, LoadRequest(), "canonical", adrDirectory);

            Assert.Equal(ExitCodes.OperationalError, load.Code);
            Assert.Contains("inside the ADR directory", load.Error, StringComparison.Ordinal);
            Assert.Equal(Canonical, File.ReadAllText(outsidePath));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void LoadDoesNotExposeHeadingsInsideLongerFencedCodeBlocks()
    {
        var root = CreateDirectory();
        var path = Path.Combine(root, "0003-fenced-example.md");
        const string markdown = """
            # Fenced example

            ## Status

            Proposed

            ## Context

            ````markdown
            ```example
            ## Not an editable section
            ```
            ````

            Real context.

            ## Decision

            Decision.

            ## Consequences

            Consequences.
            """;
        try
        {
            File.WriteAllText(path, markdown);

            var load = Run(path, LoadRequest(), "canonical");

            Assert.Equal(ExitCodes.Success, load.Code);
            var labels = JsonNode.Parse(load.Output)!["fields"]!.AsArray()
                .Select(field => field!["label"]!.GetValue<string>())
                .ToArray();
            Assert.DoesNotContain("Not an editable section", labels);
            Assert.Equal(["Status", "Context", "Decision", "Consequences"], labels);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void SaveWithoutChangesReportsThatNothingWasWritten()
    {
        var root = CreateDirectory();
        var path = Path.Combine(root, "0004-no-op.md");
        try
        {
            File.WriteAllText(path, Canonical);
            var load = Run(path, LoadRequest(), "canonical");

            var save = Run(path, SaveRequest(load.Output, "Choice", _ => { }), "canonical");

            Assert.Equal(ExitCodes.Success, save.Code);
            Assert.False(JsonNode.Parse(save.Output)!["written"]!.GetValue<bool>());
            Assert.Equal(Canonical, File.ReadAllText(path));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void SaveWithoutChangesStillValidatesTheExplicitAdrDirectory()
    {
        var root = CreateDirectory();
        var adrDirectory = Path.Combine(root, "adrs");
        var outsidePath = Path.Combine(root, "0004-outside.md");
        try
        {
            Directory.CreateDirectory(adrDirectory);
            File.WriteAllText(outsidePath, Canonical);
            var load = Run(outsidePath, LoadRequest(), "canonical");

            var save = Run(
                outsidePath,
                SaveRequest(load.Output, "Choice", _ => { }),
                "canonical",
                adrDirectory);

            Assert.Equal(ExitCodes.OperationalError, save.Code);
            Assert.Contains("inside the ADR directory", save.Error, StringComparison.Ordinal);
            Assert.Equal(Canonical, File.ReadAllText(outsidePath));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void SaveReturnsABoundedAcknowledgementAfterPersistingLargeFields()
    {
        var root = CreateDirectory();
        var path = Path.Combine(root, "0004-bounded-save.md");
        try
        {
            File.WriteAllText(path, Canonical);
            var load = Run(path, LoadRequest(), "canonical");
            var expandedContext = new string('x', 20 * 1024);
            var request = SaveRequest(load.Output, "Choice", values =>
                values[FieldId(load.Output, "Context")] = expandedContext);

            var save = Run(path, request, "canonical");

            Assert.Equal(ExitCodes.Success, save.Code);
            Assert.True(Encoding.UTF8.GetByteCount(save.Output) < 1024);
            var response = JsonNode.Parse(save.Output)!.AsObject();
            Assert.Equal(["schemaVersion", "operation", "fingerprint", "written"], response.Select(property => property.Key));
            Assert.True(response["written"]!.GetValue<bool>());
            Assert.Contains(expandedContext, File.ReadAllText(path), StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void MadrLoadIgnoresFrontMatterCommentsAndIndentedMarkdownHeadings()
    {
        var root = CreateDirectory();
        var path = Path.Combine(root, "0005-metadata.md");
        const string markdown = """
            ---
            # owner: architecture
            status: "proposed"
            date: 2026-10-12
            ---

            # Actual title

            ## Context and Problem Statement

                ## Not an editable section

            Context.

            ## Considered Options

            Option A.

            ## Decision Outcome

            Choose A.
            """;
        try
        {
            File.WriteAllText(path, markdown);

            var load = Run(path, LoadRequest(), "madr-4");

            Assert.Equal(ExitCodes.Success, load.Code);
            var response = JsonNode.Parse(load.Output)!;
            Assert.Equal("Actual title", response["title"]!.GetValue<string>());
            var labels = response["fields"]!.AsArray()
                .Select(field => field!["label"]!.GetValue<string>())
                .ToArray();
            Assert.DoesNotContain("# owner", labels);
            Assert.DoesNotContain("Not an editable section", labels);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Theory]
    [InlineData("line one\nline two")]
    [InlineData("pro\"posed")]
    public void MadrSaveRejectsUnsafeQuotedMetadataWithoutMutation(string status)
    {
        var root = CreateDirectory();
        var path = Path.Combine(root, "0006-metadata.md");
        const string markdown = """
            ---
            status: "proposed"
            date: 2026-10-12
            ---

            # Choice

            ## Context and Problem Statement

            Context.

            ## Considered Options

            Option A.

            ## Decision Outcome

            Choose A.
            """;
        try
        {
            File.WriteAllText(path, markdown);
            var load = Run(path, LoadRequest(), "madr-4");
            var request = SaveRequest(load.Output, "Choice", values =>
                values[FieldId(load.Output, "status")] = status);

            var save = Run(path, request, "madr-4");

            Assert.Equal(ExitCodes.UsageError, save.Code);
            Assert.Equal(Encoding.UTF8.GetBytes(markdown), File.ReadAllBytes(path));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void SavePreservesUtf8Bom()
    {
        var root = CreateDirectory();
        var path = Path.Combine(root, "0007-bom.md");
        try
        {
            File.WriteAllText(path, Canonical, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            var load = Run(path, LoadRequest(), "canonical");

            var save = Run(path, SaveRequest(load.Output, "Updated choice", _ => { }), "canonical");

            Assert.Equal(ExitCodes.Success, save.Code);
            Assert.True(File.ReadAllBytes(path).AsSpan().StartsWith(Encoding.UTF8.Preamble));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void EditBoundariesFailClosedWithoutMutatingTheAdr()
    {
        var root = CreateDirectory();
        var path = Path.Combine(root, "0008-boundaries.md");
        var textPath = Path.Combine(root, "not-markdown.txt");
        try
        {
            File.WriteAllText(path, Canonical);
            File.WriteAllText(textPath, Canonical);
            var load = Run(path, LoadRequest(), "canonical");
            var malformedFingerprint = SaveRequest(load.Output, "Choice", _ => { })
                .Replace(JsonNode.Parse(load.Output)!["fingerprint"]!.GetValue<string>(), new string('G', 64), StringComparison.Ordinal);
            var missingField = SaveRequest(load.Output, "Choice", values => values.Remove(FieldId(load.Output, "Context")));
            var extraField = SaveRequest(load.Output, "Choice", values => values["section-999"] = "Unexpected");
            var invalidStatus = SaveRequest(load.Output, "Choice", values =>
                values[FieldId(load.Output, "Status")] = "NotAStatus");

            Assert.Equal(ExitCodes.UsageError, Run(path, malformedFingerprint, "canonical").Code);
            Assert.Equal(ExitCodes.UsageError, Run(path, missingField, "canonical").Code);
            Assert.Equal(ExitCodes.UsageError, Run(path, extraField, "canonical").Code);
            Assert.Equal(ExitCodes.UsageError, Run(path, invalidStatus, "canonical").Code);
            Assert.Equal(ExitCodes.OperationalError, Run(textPath, LoadRequest(), "canonical").Code);
            Assert.Equal(
                ExitCodes.OperationalError,
                Run(path, new string('x', EditFormCommand.MaximumInputCharacters + 1), "canonical").Code);
            Assert.Equal(Encoding.UTF8.GetBytes(Canonical), File.ReadAllBytes(path));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void ProcessContractAcceptsRelativeTargetsAndRejectsInvalidUtf8()
    {
        var root = CreateDirectory();
        const string fileName = "0009-relative.md";
        var path = Path.Combine(root, fileName);
        try
        {
            File.WriteAllText(path, Canonical);

            var load = RunProcess(root, fileName, Encoding.UTF8.GetBytes(LoadRequest()));
            var invalidUtf8 = RunProcess(root, fileName, [0xC3, 0x28]);

            Assert.Equal(ExitCodes.Success, load.Code);
            Assert.Equal(ExitCodes.UsageError, invalidUtf8.Code);
            Assert.NotEmpty(invalidUtf8.Error);
            Assert.Equal(Canonical, File.ReadAllText(path));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void LoadRejectsOversizedInvalidAndSymbolicLinkTargets()
    {
        var root = CreateDirectory();
        var oversizedPath = Path.Combine(root, "0010-oversized.md");
        var invalidPath = Path.Combine(root, "0011-invalid.md");
        var targetPath = Path.Combine(root, "0012-target.md");
        var linkPath = Path.Combine(root, "0013-link.md");
        var invalidUtf8Path = Path.Combine(root, "0014-invalid-utf8.md");
        try
        {
            File.WriteAllText(oversizedPath, new string('x', 256 * 1024 + 1));
            File.WriteAllText(invalidPath, "No editable title.");
            File.WriteAllText(targetPath, Canonical);
            var invalidUtf8 = new byte[] { 0x23, 0x20, 0xC3, 0x28 };
            File.WriteAllBytes(invalidUtf8Path, invalidUtf8);
            var oversized = File.ReadAllBytes(oversizedPath);
            var invalid = File.ReadAllBytes(invalidPath);

            Assert.Equal(ExitCodes.OperationalError, Run(oversizedPath, LoadRequest(), "canonical").Code);
            Assert.Equal(ExitCodes.OperationalError, Run(invalidPath, LoadRequest(), "canonical").Code);
            Assert.Equal(ExitCodes.OperationalError, Run(invalidUtf8Path, LoadRequest(), "canonical").Code);
            Assert.Equal(oversized, File.ReadAllBytes(oversizedPath));
            Assert.Equal(invalid, File.ReadAllBytes(invalidPath));
            Assert.Equal(invalidUtf8, File.ReadAllBytes(invalidUtf8Path));

            try
            {
                File.CreateSymbolicLink(linkPath, targetPath);
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or PlatformNotSupportedException)
            {
                return;
            }
            Assert.Equal(ExitCodes.OperationalError, Run(linkPath, LoadRequest(), "canonical").Code);
            Assert.Equal(Encoding.UTF8.GetBytes(Canonical), File.ReadAllBytes(targetPath));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Theory]
    [InlineData("{\"schemaVersion\":\"2.0\",\"operation\":\"load\"}")]
    [InlineData("{\"schemaVersion\":\"1.0\",\"operation\":\"load\",\"title\":\"bad\"}")]
    [InlineData("{\"schemaVersion\":\"1.0\",\"operation\":\"save\"}")]
    [InlineData("{\"schemaVersion\":\"1.0\",\"operation\":\"load\",\"fields\":null}")]
    [InlineData("{\"schemaVersion\":\"1.0\",\"operation\":\"load\",\"unexpected\":true}")]
    public void InvalidContractsFailClosed(string request)
    {
        var root = CreateDirectory();
        var path = Path.Combine(root, "0001-choice.md");
        try
        {
            File.WriteAllText(path, Canonical);
            Assert.Equal(ExitCodes.UsageError, Run(path, request, "canonical").Code);
            Assert.Equal(Canonical, File.ReadAllText(path));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static string FieldId(string response, string label) => JsonNode.Parse(response)!["fields"]!.AsArray()
        .Select(item => item!.AsObject())
        .Single(field => field["label"]!.GetValue<string>() == label)["id"]!.GetValue<string>();

    private static string SaveRequest(string loadResponse, string title, Action<JsonObject> update)
    {
        var load = JsonNode.Parse(loadResponse)!.AsObject();
        var values = new JsonObject();
        foreach (var field in load["fields"]!.AsArray())
            values[field!["id"]!.GetValue<string>()] = field["value"]!.GetValue<string>();
        update(values);
        return JsonSerializer.Serialize(new
        {
            schemaVersion = "1.0",
            operation = "save",
            fingerprint = load["fingerprint"]!.GetValue<string>(),
            title,
            fields = values,
        });
    }

    private static string LoadRequest() => """{"schemaVersion":"1.0","operation":"load"}""";

    private static (int Code, string Output, string Error) Run(
        string path,
        string request,
        string format,
        string? adrDirectory = null)
    {
        using var input = new StringReader(request);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var args = new List<string> { "edit", path, "--form", "--adr-format", format };
        if (adrDirectory is not null)
        {
            args.Add("--adr-directory");
            args.Add(adrDirectory);
        }
        var code = CliApplication.Run(
            args,
            input,
            output,
            error,
            TestContext.Current.CancellationToken);
        return (code, output.ToString(), error.ToString());
    }

    private static (int Code, string Output, string Error) RunProcess(
        string workingDirectory,
        string path,
        byte[] input)
    {
        var startInfo = new ProcessStartInfo(ExecutablePathResolver.Resolve("dotnet"))
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(typeof(CliApplication).Assembly.Location);
        startInfo.ArgumentList.Add("edit");
        startInfo.ArgumentList.Add(path);
        startInfo.ArgumentList.Add("--form");
        startInfo.ArgumentList.Add("--adr-format");
        startInfo.ArgumentList.Add("canonical");
        using var process = Process.Start(startInfo)!;
        process.StandardInput.BaseStream.Write(input);
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output, error);
    }

    private static string CreateDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "adr-guard-edit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }

    private const string Canonical = """
        # Choice

        ## Status

        Proposed

        ## Context

        Context.

        ## Decision

        Decision.

        ## Consequences

        Consequences.
        """;
}
