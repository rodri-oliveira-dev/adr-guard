using System.Text.Json;
using Xunit;

namespace AdrGuard.Tests.Drift;

public sealed class FitnessContractFixtureTests
{
    [Fact]
    public void VersionedManifestFixtureDeclaresNarrowAuditableRules()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Drift", "contract");
        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Schemas", "adr-fitness-rules-v1.schema.json")));
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".adrguard-fitness.json")));

        Assert.Equal("1.0", schema.RootElement.GetProperty("properties").GetProperty("schemaVersion").GetProperty("const").GetString());
        Assert.False(schema.RootElement.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal("1.0", manifest.RootElement.GetProperty("schemaVersion").GetString());

        var rules = manifest.RootElement.GetProperty("rules").EnumerateArray().ToArray();
        Assert.Equal(["domain-no-infrastructure", "architecture-tests"], rules.Select(rule => rule.GetProperty("id").GetString()).ToArray());
        Assert.Equal(["forbid-project-reference", "external-test"], rules.Select(rule => rule.GetProperty("check").GetProperty("type").GetString()).ToArray());
        Assert.All(rules, rule =>
        {
            Assert.Equal("unknown", rule.GetProperty("failure").GetProperty("onInconclusive").GetString());
            var decisionPath = rule.GetProperty("decision").GetProperty("path").GetString()!;
            Assert.True(File.Exists(Path.Combine(root, decisionPath.Replace('/', Path.DirectorySeparatorChar))));
        });
    }
}
