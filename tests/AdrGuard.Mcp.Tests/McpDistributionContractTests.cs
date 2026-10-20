using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Xunit;

namespace AdrGuard.Mcp.Tests;

public sealed class McpDistributionContractTests
{
    private const string ExpectedName = "io.github.rodri-oliveira-dev/adr-guard";
    private const string ExpectedPackage = "RodriOliveira.AdrGuard.Mcp";
    private const string ExpectedCandidateVersion = "1.0.1";
    private const string PublishedManifestVersion = "1.0.0";
    private const string ExpectedSchema = "https://static.modelcontextprotocol.io/schemas/2025-12-11/server.schema.json";

    [Fact]
    public void CheckedInManifestPreservesPublishedVersionAndReleaseGeneratesCandidate()
    {
        var root = FindRepositoryRoot();
        var manifest = LoadManifest(root);
        var readme = File.ReadAllText(Path.Combine(root, "src", "AdrGuard.Mcp", "README.md"));
        var project = XDocument.Load(Path.Combine(root, "src", "AdrGuard.Mcp", "AdrGuard.Mcp.csproj"));
        var candidateVersion = project.Descendants("VersionPrefix").Single().Value;
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "mcp-release.yml"));

        Assert.Equal(ExpectedCandidateVersion, candidateVersion);
        Assert.Empty(Validate(manifest, PublishedManifestVersion, readme));
        Assert.Contains("jq --arg version \"$VERSION\" '.version = $version | .packages = (.packages | map(.version = $version))'", workflow, StringComparison.Ordinal);
        Assert.Contains("test \"$(jq -r '.version' .mcp/server.json)\" = \"$VERSION\"", workflow, StringComparison.Ordinal);
        Assert.Contains("test \"$(jq -r '.packages[0].version' .mcp/server.json)\" = \"$VERSION\"", workflow, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("name")]
    [InlineData("version")]
    [InlineData("package")]
    [InlineData("transport")]
    [InlineData("marker")]
    public void InvalidDistributionContractsAreRejected(string mutation)
    {
        var root = FindRepositoryRoot();
        var manifest = LoadManifest(root);
        var readme = File.ReadAllText(Path.Combine(root, "src", "AdrGuard.Mcp", "README.md"));
        var version = PublishedManifestVersion;

        switch (mutation)
        {
            case "schema": manifest["$schema"] = "https://example.invalid/schema.json"; break;
            case "name": manifest["name"] = "io.github.example/wrong"; break;
            case "version": manifest["packages"]![0]!["version"] = "9.9.9"; break;
            case "package": manifest["packages"]![0]!["identifier"] = "Wrong.Package"; break;
            case "transport": manifest["packages"]![0]!["transport"]!["type"] = "sse"; break;
            case "marker": readme = readme.Replace(ExpectedName, "io.github.example/wrong", StringComparison.Ordinal); break;
        }

        Assert.NotEmpty(Validate(manifest, version, readme));
    }

    [Fact]
    public void ReleaseWorkflowIsManualMainOnlyAndUsesScopedOidc()
    {
        var workflow = File.ReadAllText(Path.Combine(FindRepositoryRoot(), ".github", "workflows", "mcp-release.yml"));

        Assert.Contains("workflow_dispatch:", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("pull_request_target:", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("tags:", workflow, StringComparison.Ordinal);
        Assert.Contains("github.ref == 'refs/heads/main'", workflow, StringComparison.Ordinal);
        Assert.Contains("NuGet/login@8d196754b4036150537f80ac539e15c2f1028841", workflow, StringComparison.Ordinal);
        Assert.Contains("mcp-publisher login github-oidc", workflow, StringComparison.Ordinal);
        Assert.Contains("id-token: write", workflow, StringComparison.Ordinal);
        Assert.Contains("mcp-publisher\" validate .mcp/server.json", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void PackedToolContainsRuntimeDependenciesReadmeAndOwnershipMarker()
    {
        var packagePath = Environment.GetEnvironmentVariable("ADR_GUARD_MCP_PACKAGE");
        if (string.IsNullOrWhiteSpace(packagePath)) return;

        using var archive = ZipFile.OpenRead(packagePath);
        var names = archive.Entries.Select(entry => entry.FullName).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("README.md", names);
        Assert.Contains("CHANGELOG.md", names);
        Assert.Contains("LICENSE", names);
        Assert.Contains("tools/net10.0/any/adr-guard-mcp.dll", names);
        Assert.Contains("tools/net10.0/any/AdrGuard.Core.dll", names);
        Assert.DoesNotContain("tools/net10.0/any/adr-guard.dll", names);
        Assert.Contains("tools/net10.0/any/adr-guard-mcp.deps.json", names);
        Assert.Contains("tools/net10.0/any/DotnetToolSettings.xml", names);

        using var reader = new StreamReader(archive.GetEntry("README.md")!.Open());
        Assert.Contains($"<!-- mcp-name: {ExpectedName} -->", reader.ReadToEnd(), StringComparison.Ordinal);

        var nuspec = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        using var nuspecReader = new StreamReader(nuspec.Open());
        var metadata = XDocument.Parse(nuspecReader.ReadToEnd());
        Assert.Equal(ExpectedPackage, metadata.Descendants().Single(element => element.Name.LocalName == "id").Value);
        Assert.Equal(ExpectedCandidateVersion, metadata.Descendants().Single(element => element.Name.LocalName == "version").Value);
        Assert.Equal("README.md", metadata.Descendants().Single(element => element.Name.LocalName == "readme").Value);
    }

    private static JsonObject LoadManifest(string root) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(root, ".mcp", "server.json")))!.AsObject();

    private static List<string> Validate(JsonObject manifest, string projectVersion, string readme)
    {
        var issues = new List<string>();
        var package = manifest["packages"]?[0];
        if (manifest["$schema"]?.GetValue<string>() != ExpectedSchema) issues.Add("schema");
        if (manifest["name"]?.GetValue<string>() != ExpectedName) issues.Add("name");
        if (manifest["version"]?.GetValue<string>() != projectVersion) issues.Add("manifest version");
        if (package?["version"]?.GetValue<string>() != projectVersion) issues.Add("package version");
        if (package?["identifier"]?.GetValue<string>() != ExpectedPackage) issues.Add("package identifier");
        if (package?["registryType"]?.GetValue<string>() != "nuget") issues.Add("registry type");
        if (package?["registryBaseUrl"]?.GetValue<string>() != "https://api.nuget.org/v3/index.json") issues.Add("registry URL");
        if (package?["transport"]?["type"]?.GetValue<string>() != "stdio") issues.Add("transport");
        if (!readme.Contains($"<!-- mcp-name: {ExpectedName} -->", StringComparison.Ordinal)) issues.Add("ownership marker");
        return issues;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AdrGuard.slnx"))) return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the ADR Guard repository root.");
    }
}
