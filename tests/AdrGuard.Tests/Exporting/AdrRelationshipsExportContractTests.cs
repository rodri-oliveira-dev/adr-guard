using System.Text.Json;
using AdrGuard.Cli;
using AdrGuard.Exporting;
using AdrGuard.Parsing;
using AdrGuard.Validation;
using Xunit;

namespace AdrGuard.Tests.Exporting;

public sealed class AdrRelationshipsExportContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void SerializerWritesStableCamelCaseContractAndExplicitNulls()
    {
        var export = new AdrRelationshipsExport(
            AdrRelationshipsExportContract.SchemaVersion,
            [new("ADR-2", "docs/adr/0002-proposed.md", "Proposed decision", AdrRelationshipExportStatus.Proposed)],
            [new(
                "ADR-2",
                "docs/adr/0002-proposed.md",
                AdrRelationshipExportType.DependsOn,
                "0099-missing.md",
                null,
                null,
                AdrRelationshipExportResolution.Unresolved)],
            [new(
                "ADR007",
                "ADR relationship '0099-missing.md' does not resolve to an ADR in the validated set.",
                "docs/adr/0002-proposed.md",
                AdrRelationshipExportIssueSeverity.Error)]);
        using var output = new StringWriter();

        AdrRelationshipsExportSerializer.Write(export, output);

        Assert.Equal(
            """
            {
              "schemaVersion": "1.0",
              "adrs": [
                {
                  "identifier": "ADR-2",
                  "path": "docs/adr/0002-proposed.md",
                  "title": "Proposed decision",
                  "status": "Proposed"
                }
              ],
              "relationships": [
                {
                  "sourceIdentifier": "ADR-2",
                  "sourcePath": "docs/adr/0002-proposed.md",
                  "type": "depends-on",
                  "declaredTarget": "0099-missing.md",
                  "targetIdentifier": null,
                  "targetPath": null,
                  "resolution": "unresolved"
                }
              ],
              "issues": [
                {
                  "code": "ADR007",
                  "message": "ADR relationship \u00270099-missing.md\u0027 does not resolve to an ADR in the validated set.",
                  "path": "docs/adr/0002-proposed.md",
                  "severity": "error"
                }
              ]
            }

            """,
            output.ToString());
    }

    [Fact]
    public void UnknownStatusCannotBeDeserializedAsAccepted()
    {
        const string json = """
            {
              "schemaVersion": "1.0",
              "adrs": [{"identifier":"ADR-1","path":"docs/adr/0001-invalid.md","title":"Invalid","status":"Approved"}],
              "relationships": [],
              "issues": []
            }
            """;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AdrRelationshipsExport>(json, JsonOptions));
    }

    [Fact]
    public void VersionedFixturesCoverRequiredIntegrationScenarios()
    {
        var fixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "RelationshipsExport");
        var fixtureNames = Directory.EnumerateFiles(fixtureRoot, "*.json")
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            [
                "adr-statuses.json",
                "bidirectional-declarations.json",
                "empty-graph.json",
                "invalid-relationships.json",
                "unresolved-references.json",
                "valid-graph.json",
            ],
            fixtureNames);

        foreach (var fixtureName in fixtureNames)
        {
            var json = File.ReadAllText(Path.Combine(fixtureRoot, fixtureName!));
            var export = JsonSerializer.Deserialize<AdrRelationshipsExport>(json, JsonOptions);
            Assert.NotNull(export);
            using var serialized = new StringWriter();
            AdrRelationshipsExportSerializer.Write(export, serialized);
            Assert.Equal(json, serialized.ToString());
            Assert.Equal(AdrRelationshipsExportContract.SchemaVersion, export.SchemaVersion);
            Assert.DoesNotContain("generatedAt", json, StringComparison.Ordinal);
            Assert.DoesNotContain("markdown", json, StringComparison.OrdinalIgnoreCase);
            Assert.All(export.Adrs, adr => AssertRepositoryRelativePath(adr.Path));
            Assert.All(
                export.Relationships,
                relationship =>
                {
                    AssertRepositoryRelativePath(relationship.SourcePath);
                    if (relationship.TargetPath is not null) AssertRepositoryRelativePath(relationship.TargetPath);
                    if (relationship.Resolution is AdrRelationshipExportResolution.Ambiguous or AdrRelationshipExportResolution.Unresolved)
                    {
                        Assert.Null(relationship.TargetIdentifier);
                        Assert.Null(relationship.TargetPath);
                    }
                });
            Assert.All(export.Issues, issue => AssertRepositoryRelativePath(issue.Path));
        }

        var statuses = ReadFixture(fixtureRoot, "adr-statuses.json");
        Assert.Equal(
            [
                AdrRelationshipExportStatus.Accepted,
                AdrRelationshipExportStatus.Proposed,
                AdrRelationshipExportStatus.Deprecated,
                AdrRelationshipExportStatus.Superseded,
                null,
            ],
            statuses.Adrs.Select(adr => adr.Status));
        Assert.Contains(statuses.Issues, issue => issue.Code == ValidationCodes.InvalidStatus);

        var reciprocal = ReadFixture(fixtureRoot, "bidirectional-declarations.json");
        Assert.Equal(
            [AdrRelationshipExportType.SupersededBy, AdrRelationshipExportType.Supersedes],
            reciprocal.Relationships.Select(relationship => relationship.Type));

        var unresolved = ReadFixture(fixtureRoot, "unresolved-references.json");
        Assert.Equal(AdrRelationshipExportResolution.Unresolved, Assert.Single(unresolved.Relationships).Resolution);

        var invalid = ReadFixture(fixtureRoot, "invalid-relationships.json");
        Assert.Equal(2, invalid.Issues.Count(issue => issue.Code == ValidationCodes.SupersessionCycle));

        var empty = ReadFixture(fixtureRoot, "empty-graph.json");
        Assert.Empty(empty.Adrs);
        Assert.Empty(empty.Relationships);
        Assert.Empty(empty.Issues);
    }

    [Fact]
    public void FixtureDiagnosticsMatchCoreRelationshipValidation()
    {
        var first = Parse(
            "0001-first.md",
            Canonical("First decision", "Superseded", "## Superseded by\n[Second](0002-second.md)"));
        var second = Parse(
            "0002-second.md",
            Canonical("Second decision", "Superseded", "## Superseded by\n[First](0001-first.md)"));
        var cycleIssues = AdrValidator.Validate([first, second]).Issues
            .Where(issue => issue.Code == ValidationCodes.SupersessionCycle)
            .ToArray();
        var fixture = ReadFixture(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "RelationshipsExport"),
            "invalid-relationships.json");

        Assert.Equal(
            cycleIssues.Select(issue => (issue.Code, issue.Message)),
            fixture.Issues.Select(issue => (issue.Code, issue.Message)));

        var unresolved = Parse(
            "0001-service.md",
            Canonical("Build the service boundary", "Accepted", "## Depends on\n[Missing](0099-missing.md)"));
        var brokenReference = Assert.Single(
            AdrValidator.Validate([unresolved]).Issues,
            issue => issue.Code == ValidationCodes.BrokenReference);
        var unresolvedFixture = ReadFixture(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "RelationshipsExport"),
            "unresolved-references.json");
        var fixtureIssue = Assert.Single(unresolvedFixture.Issues);
        Assert.Equal((brokenReference.Code, brokenReference.Message), (fixtureIssue.Code, fixtureIssue.Message));

        var invalidStatus = Parse(
            "0005-invalid-status.md",
            Canonical("Decision with invalid status", "Approved", string.Empty));
        var invalidStatusIssue = Assert.Single(
            AdrValidator.Validate([invalidStatus]).Issues,
            issue => issue.Code == ValidationCodes.InvalidStatus);
        var statusFixture = ReadFixture(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "RelationshipsExport"),
            "adr-statuses.json");
        var statusFixtureIssue = Assert.Single(statusFixture.Issues);
        Assert.Equal(
            (invalidStatusIssue.Code, invalidStatusIssue.Message),
            (statusFixtureIssue.Code, statusFixtureIssue.Message));
    }

    [Fact]
    public void SchemaFreezesFieldValueAndVersionRules()
    {
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Schemas", "adr-relationships-export-v1.schema.json");
        using var schema = JsonDocument.Parse(File.ReadAllText(schemaPath));
        var root = schema.RootElement;

        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal("1.0", root.GetProperty("properties").GetProperty("schemaVersion").GetProperty("const").GetString());
        Assert.Equal(
            ["schemaVersion", "adrs", "relationships", "issues"],
            root.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(
            ["resolved", "ambiguous", "unresolved"],
            root.GetProperty("$defs").GetProperty("relationship").GetProperty("properties")
                .GetProperty("resolution").GetProperty("enum").EnumerateArray().Select(item => item.GetString()));
    }

    private static AdrRelationshipsExport ReadFixture(string fixtureRoot, string name) =>
        JsonSerializer.Deserialize<AdrRelationshipsExport>(
            File.ReadAllText(Path.Combine(fixtureRoot, name)),
            JsonOptions)!;

    private static void AssertRepositoryRelativePath(string path)
    {
        Assert.False(Path.IsPathRooted(path));
        Assert.DoesNotContain('\\', path);
        Assert.DoesNotContain("../", path, StringComparison.Ordinal);
        Assert.StartsWith("docs/adr/", path, StringComparison.Ordinal);
    }

    private static AdrGuard.Model.AdrDocument Parse(string path, string markdown) =>
        AdrMarkdownParser.Parse(Path.Combine("docs", "adr", path), markdown);

    private static string Canonical(string title, string status, string relationship) =>
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
        {relationship}
        """;
}
