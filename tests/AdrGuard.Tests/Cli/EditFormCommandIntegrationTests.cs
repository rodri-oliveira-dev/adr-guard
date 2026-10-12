using System.Text.Json;
using System.Text.Json.Nodes;
using AdrGuard.Cli;
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

    private static (int Code, string Output, string Error) Run(string path, string request, string format)
    {
        using var input = new StringReader(request);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = CliApplication.Run(
            ["edit", path, "--form", "--adr-format", format],
            input,
            output,
            error,
            TestContext.Current.CancellationToken);
        return (code, output.ToString(), error.ToString());
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
