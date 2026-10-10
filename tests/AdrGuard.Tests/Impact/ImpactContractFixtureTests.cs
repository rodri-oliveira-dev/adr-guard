using System.Text.Json;
using Xunit;

namespace AdrGuard.Tests.Impact;

public sealed class ImpactContractFixtureTests
{
    [Fact]
    public void VersionedManifestFixtureMatchesPublishedContractSkeleton()
    {
        var fixtureRoot = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Impact",
            "case-study");
        var manifestPath = Path.Combine(
            fixtureRoot,
            ".adrguard-impact.json");
        var schemaPath = Path.Combine(
            AppContext.BaseDirectory,
            "Schemas",
            "adr-impact-map-v1.schema.json");

        using var schema = JsonDocument.Parse(File.ReadAllText(schemaPath));
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));

        Assert.Equal(
            "1.0",
            schema.RootElement
                .GetProperty("properties")
                .GetProperty("schemaVersion")
                .GetProperty("const")
                .GetString());
        Assert.False(
            schema.RootElement
                .GetProperty("additionalProperties")
                .GetBoolean());

        var root = manifest.RootElement;
        Assert.Equal("1.0", root.GetProperty("schemaVersion").GetString());

        var properties = root.EnumerateObject().Select(property => property.Name).ToArray();
        Assert.Equal(["schemaVersion", "mappings"], properties);

        var mappings = root.GetProperty("mappings").EnumerateArray().ToArray();
        Assert.Equal(3, mappings.Length);
        Assert.Equal(
            ["Accepted", "Proposed", "Rejected"],
            mappings.Select(mapping => ReadStatus(fixtureRoot, mapping)).ToArray());
        Assert.Equal(
            ["ADR-1", "ADR-2", "ADR-3"],
            mappings.Select(mapping => mapping.GetProperty("decision").GetProperty("stableId").GetString()).ToArray());

        Assert.All(
            mappings,
            mapping =>
            {
                var decision = mapping.GetProperty("decision");
                var relativePath = decision.GetProperty("path").GetString();
                Assert.NotNull(relativePath);
                Assert.DoesNotContain('\\', relativePath);
                Assert.True(File.Exists(Path.Combine(fixtureRoot, relativePath.Replace('/', Path.DirectorySeparatorChar))));
                Assert.NotEmpty(mapping.GetProperty("patterns").EnumerateArray());
                Assert.False(string.IsNullOrWhiteSpace(mapping.GetProperty("relationship").GetString()));
                Assert.False(string.IsNullOrWhiteSpace(mapping.GetProperty("reason").GetString()));
            });
    }

    private static string ReadStatus(string fixtureRoot, JsonElement mapping)
    {
        var relativePath = mapping
            .GetProperty("decision")
            .GetProperty("path")
            .GetString()!;
        var markdown = File.ReadAllText(
            Path.Combine(
                fixtureRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var marker = "## Status";
        var statusIndex = markdown.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(statusIndex >= 0);
        return markdown[(statusIndex + marker.Length)..]
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
    }
}
