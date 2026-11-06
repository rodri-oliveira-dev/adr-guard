using System.Text.Json;
using System.Text.Json.Nodes;
using AdrGuard.Cli;
using Json.Schema;
using Xunit;

namespace AdrGuard.Tests.Cli;

public sealed class RelationshipsCommandIntegrationTests
{
    [Fact]
    public void PublishedSchemaValidatesReportsAndRejectsExtraNodeProperties()
    {
        using var repository = new TestRepository();
        repository.Write("0001-first.md", Canonical("First", "Accepted"));
        var result = repository.Run(["relationships"]);
        Assert.Equal(ExitCodes.Success, result.ExitCode);

        var schema = LoadPublishedSchema();
        Assert.True(Evaluate(schema, result.Output).IsValid);

        var withExtraProperty = JsonNode.Parse(result.Output)!.AsObject();
        withExtraProperty["nodes"]!.AsArray()[0]!.AsObject()["unexpected"] = true;
        Assert.False(Evaluate(schema, withExtraProperty.ToJsonString()).IsValid);
    }

    [Fact]
    public void ValidCanonicalGraphIsDeterministicAndReadOnly()
    {
        using var repository = new TestRepository();
        repository.Write("docs/adr/0002-database.md", Canonical("Database", "Accepted"));
        repository.Write(
            "docs/adr/0001-api.md",
            Canonical("API", "Accepted", "## Depends on\n\n- [Database](0002-database.md)\n"));
        var before = repository.Snapshot();

        var first = repository.Run(["relationships", "docs/adr", "--format", "json"]);
        var second = repository.Run(["relationships", "docs/adr", "--format", "json"]);

        Assert.Equal(ExitCodes.Success, first.ExitCode);
        Assert.Equal(string.Empty, first.Error);
        Assert.Equal(first.Output, second.Output);
        Assert.Equal(before, repository.Snapshot());
        using var json = JsonDocument.Parse(first.Output);
        Assert.Equal("1.0", json.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal(2, json.RootElement.GetProperty("nodes").GetArrayLength());
        var api = json.RootElement.GetProperty("nodes").EnumerateArray()
            .Single(node => node.GetProperty("id").GetString() == "ADR-1");
        Assert.Equal("API", api.GetProperty("title").GetString());
        Assert.Equal("Accepted", api.GetProperty("status").GetString());
        Assert.Equal("docs/adr/0001-api.md", api.GetProperty("path").GetString());
        Assert.Equal("canonical", api.GetProperty("format").GetString());
        var relationship = Assert.Single(json.RootElement.GetProperty("relationships").EnumerateArray());
        Assert.Equal("depends-on", relationship.GetProperty("type").GetString());
        Assert.Equal("resolved", relationship.GetProperty("resolution").GetString());
        Assert.Equal("docs/adr/0002-database.md", relationship.GetProperty("targets")[0].GetProperty("path").GetString());
    }

    [Fact]
    public void CycleReturnsGraphAndAdr010Diagnostics()
    {
        using var repository = new TestRepository();
        repository.Write("0001-first.md", Canonical("First", "Superseded", "## Superseded by\n\n- [Second](0002-second.md)\n"));
        repository.Write("0002-second.md", Canonical("Second", "Superseded", "## Superseded by\n\n- [First](0001-first.md)\n"));

        var result = repository.Run(["relationships"]);

        Assert.Equal(ExitCodes.ValidationFailed, result.ExitCode);
        using var json = JsonDocument.Parse(result.Output);
        Assert.Equal(2, json.RootElement.GetProperty("relationships").GetArrayLength());
        Assert.All(
            json.RootElement.GetProperty("diagnostics").EnumerateArray(),
            diagnostic => Assert.Equal("ADR010", diagnostic.GetProperty("code").GetString()));
    }

    [Fact]
    public void MissingAndUnsafeTargetsRemainUnresolvedDiagnostics()
    {
        using var repository = new TestRepository();
        repository.Write(
            "docs/adr/0001-first.md",
            Canonical(
                "First",
                "Accepted",
                "## Depends on\n\n- [Missing](missing.md)\n- [Outside](../../../outside.md)\n"));

        var result = repository.Run(["relationships", "docs/adr"]);

        Assert.Equal(ExitCodes.ValidationFailed, result.ExitCode);
        using var json = JsonDocument.Parse(result.Output);
        var relationships = json.RootElement.GetProperty("relationships").EnumerateArray().ToArray();
        Assert.Equal(2, relationships.Length);
        Assert.All(relationships, edge => Assert.Equal("unresolved", edge.GetProperty("resolution").GetString()));
        Assert.All(
            json.RootElement.GetProperty("diagnostics").EnumerateArray(),
            diagnostic => Assert.Equal("ADR007", diagnostic.GetProperty("code").GetString()));
    }

    [Fact]
    public void MadrDuplicateTargetIsAmbiguousAndPreservesCandidatePaths()
    {
        using var repository = new TestRepository();
        repository.Write("0001-source.md", Madr("Source", "superseded by ADR-0002"));
        repository.Write("0002-first.md", Madr("First", "accepted"));
        repository.Write("0002-second.md", Madr("Second", "accepted"));

        var result = repository.Run(["relationships", "--adr-format", "madr-4"]);

        Assert.Equal(ExitCodes.ValidationFailed, result.ExitCode);
        using var json = JsonDocument.Parse(result.Output);
        var relationship = Assert.Single(json.RootElement.GetProperty("relationships").EnumerateArray());
        Assert.Equal("ambiguous", relationship.GetProperty("resolution").GetString());
        Assert.Equal(2, relationship.GetProperty("targets").GetArrayLength());
        Assert.Contains(
            json.RootElement.GetProperty("diagnostics").EnumerateArray(),
            diagnostic => diagnostic.GetProperty("code").GetString() == "ADR006");
    }

    [Fact]
    public void ValidMadrSupersessionResolvesToOneDeclaredTarget()
    {
        using var repository = new TestRepository();
        repository.Write("0001-source.md", Madr("Source", "superseded by ADR-0002"));
        repository.Write("0002-target.md", Madr("Target", "accepted"));

        var result = repository.Run(["relationships", "--adr-format", "madr-4"]);

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        using var json = JsonDocument.Parse(result.Output);
        var relationship = Assert.Single(json.RootElement.GetProperty("relationships").EnumerateArray());
        Assert.Equal("resolved", relationship.GetProperty("resolution").GetString());
        Assert.Equal("madr-4", json.RootElement.GetProperty("nodes")[0].GetProperty("format").GetString());
    }

    [Fact]
    public void DirectoryOutsideRepositoryFailsClosed()
    {
        using var repository = new TestRepository();
        using var outside = new TestRepository();
        outside.Write("0001-outside.md", Canonical("Outside", "Accepted"));

        var result = repository.Run(["relationships", outside.Root]);

        Assert.Equal(ExitCodes.OperationalError, result.ExitCode);
        Assert.Equal(string.Empty, result.Output);
        Assert.Contains("stay inside the repository root", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void FileSetOverLimitFailsBeforeProducingPartialJson()
    {
        using var repository = new TestRepository();
        for (var index = 1; index <= RelationshipsCommand.MaximumAdrFiles + 1; index++)
        {
            repository.Write($"{index:D4}-decision.md", Canonical($"Decision {index}", "Accepted"));
        }

        var result = repository.Run(["relationships"]);

        Assert.Equal(ExitCodes.OperationalError, result.ExitCode);
        Assert.Equal(string.Empty, result.Output);
        Assert.Contains($"{RelationshipsCommand.MaximumAdrFiles}-file limit", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void MaximumFileSetProducesACompleteBoundedGraph()
    {
        using var repository = new TestRepository();
        for (var index = 1; index <= RelationshipsCommand.MaximumAdrFiles; index++)
        {
            repository.Write($"{index:D4}-decision.md", Canonical($"Decision {index}", "Accepted"));
        }

        var result = repository.Run(["relationships"]);

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(result.Output) <= RelationshipGraphReportRenderer.MaximumResponseBytes);
        using var json = JsonDocument.Parse(result.Output);
        Assert.Equal(RelationshipsCommand.MaximumAdrFiles, json.RootElement.GetProperty("nodes").GetArrayLength());
        Assert.Equal(0, json.RootElement.GetProperty("relationships").GetArrayLength());
    }

    [Fact]
    public void ResponseOverLimitFailsBeforeProducingPartialJson()
    {
        using var repository = new TestRepository();
        var longTitle = new string('x', 400_000);
        repository.Write("0001-first.md", Canonical(longTitle, "Accepted"));
        repository.Write("0002-second.md", Canonical(longTitle, "Accepted"));
        repository.Write("0003-third.md", Canonical(longTitle, "Accepted"));

        var result = repository.Run(["relationships"]);

        Assert.Equal(ExitCodes.OperationalError, result.ExitCode);
        Assert.Equal(string.Empty, result.Output);
        Assert.Contains(
            $"{RelationshipGraphReportRenderer.MaximumResponseBytes}-byte limit",
            result.Error,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PreCanceledOperationFailsWithoutProducingPartialJson()
    {
        using var repository = new TestRepository();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.True(RelationshipsCommandArguments.TryParse(["relationships"], out var arguments));
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = RelationshipsCommand.Run(
            arguments,
            repository.Root,
            output,
            error,
            cancellation.Token);

        Assert.Equal(ExitCodes.OperationalError, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("canceled", error.ToString(), StringComparison.Ordinal);
    }

    private static JsonSchema LoadPublishedSchema()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Schemas",
            "adr-relationship-graph-v1.schema.json");
        return JsonSchema.FromText(File.ReadAllText(path));
    }

    private static EvaluationResults Evaluate(JsonSchema schema, string json)
    {
        using var document = JsonDocument.Parse(json);
        return schema.Evaluate(
            document.RootElement,
            new EvaluationOptions { OutputFormat = OutputFormat.List });
    }

    private static string Canonical(string title, string status, string relationships = "") => $$"""
        # {{title}}

        ## Status
        {{status}}

        ## Context
        Context.

        ## Decision
        Decision.

        ## Consequences
        Consequences.

        {{relationships}}
        """;

    private static string Madr(string title, string status) => $$"""
        ---
        status: {{status}}
        ---

        # {{title}}

        ## Context and Problem Statement
        Context.

        ## Considered Options
        Options.

        ## Decision Outcome
        Outcome.
        """;

    private sealed class TestRepository : IDisposable
    {
        internal TestRepository() => Root = Directory.CreateTempSubdirectory("adr-relationships-").FullName;

        internal string Root { get; }

        internal void Write(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        internal (int ExitCode, string Output, string Error) Run(string[] arguments)
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            var exitCode = CliApplication.Run(arguments, output, error, invocationDirectory: Root);
            return (exitCode, output.ToString(), error.ToString());
        }

        internal string[] Snapshot() => Directory.GetFiles(Root, "*", SearchOption.AllDirectories)
            .Select(path => $"{Path.GetRelativePath(Root, path)}:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
