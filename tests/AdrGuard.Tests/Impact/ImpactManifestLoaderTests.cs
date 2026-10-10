using AdrGuard.Impact;
using Xunit;

namespace AdrGuard.Tests.Impact;

public sealed class ImpactManifestLoaderTests
{
    [Fact]
    public void LoadsStrictManifestAndClassifiesLifecycleWithoutInferringApproval()
    {
        using var repository = TemporaryImpactRepository.Create();
        repository.Write("docs/adr/0001-active.md", Adr("Accepted"));
        repository.Write("docs/adr/0002-proposed.md", Adr("Proposed"));
        repository.Write("docs/adr/ADR-0003-rejected.md", Adr("Rejected"));
        repository.WriteManifest(
            Mapping("ADR-1", "docs/adr/0001-active.md", "src/api/**"),
            Mapping("ADR-2", "docs/adr/0002-proposed.md", "src/migration/**", "implements"),
            Mapping("ADR-3", "docs/adr/ADR-0003-rejected.md", "src/legacy/**", "constrains"));

        var manifest = ImpactManifestLoader.Load(
            repository.Root,
            ".adrguard-impact.json");

        Assert.Equal("1.0", manifest.SchemaVersion);
        Assert.Equal(
            [
                ImpactDecisionResolutionKind.Active,
                ImpactDecisionResolutionKind.Proposed,
                ImpactDecisionResolutionKind.Inactive,
            ],
            manifest.Mappings.Select(mapping => mapping.Resolution.Kind).ToArray());
        Assert.Equal(
            ["ADR-1", "ADR-2", "ADR-3"],
            manifest.Mappings.Select(mapping => mapping.Decision.StableId).ToArray());
    }

    [Theory]
    [InlineData("2.0", "Unsupported impact manifest schema version")]
    [InlineData("", "Unsupported impact manifest schema version")]
    public void RejectsUnsupportedOrEmptySchemaVersion(
        string schemaVersion,
        string expected)
    {
        using var repository = TemporaryImpactRepository.Create();
        repository.WriteManifestRaw(
            $$"""
            {"schemaVersion":"{{schemaVersion}}","mappings":[{{Mapping("ADR-1", "docs/adr/0001.md", "src/**")}}]}
            """);

        var exception = Assert.Throws<ImpactManifestException>(
            () => ImpactManifestLoader.Load(repository.Root, ".adrguard-impact.json"));

        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("unknown", "unsupported properties")]
    [InlineData("schemaVersion", "duplicated")]
    public void RejectsUnknownAndDuplicateFields(string kind, string expected)
    {
        using var repository = TemporaryImpactRepository.Create();
        var json = kind == "unknown"
            ? "{\"schemaVersion\":\"1.0\",\"mappings\":[],\"unknown\":true}"
            : "{\"schemaVersion\":\"1.0\",\"schemaVersion\":\"1.0\",\"mappings\":[]}";
        repository.WriteManifestRaw(json);

        var exception = Assert.Throws<ImpactManifestException>(
            () => ImpactManifestLoader.Load(repository.Root, ".adrguard-impact.json"));

        Assert.Contains(expected, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsMalformedInvalidUtf8AndOversizedManifests()
    {
        using var malformed = TemporaryImpactRepository.Create();
        malformed.WriteManifestRaw("{");
        Assert.Throws<ImpactManifestException>(
            () => ImpactManifestLoader.Load(malformed.Root, ".adrguard-impact.json"));

        using var invalidUtf8 = TemporaryImpactRepository.Create();
        invalidUtf8.WriteManifestBytes([0xC3, 0x28]);
        Assert.Contains(
            "valid UTF-8",
            Assert.Throws<ImpactManifestException>(
                () => ImpactManifestLoader.Load(invalidUtf8.Root, ".adrguard-impact.json")).Message,
            StringComparison.Ordinal);

        using var oversized = TemporaryImpactRepository.Create();
        oversized.WriteManifestBytes(new byte[ImpactManifestLoader.MaximumManifestBytes + 1]);
        Assert.Contains(
            "byte limit",
            Assert.Throws<ImpactManifestException>(
                () => ImpactManifestLoader.Load(oversized.Root, ".adrguard-impact.json")).Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../outside.md")]
    [InlineData("/absolute.json")]
    [InlineData("C:\\absolute.json")]
    public void RejectsManifestTraversalAndAbsolutePaths(string path)
    {
        using var repository = TemporaryImpactRepository.Create();

        Assert.Throws<ImpactManifestException>(
            () => ImpactManifestLoader.Load(repository.Root, path));
    }

    [Theory]
    [InlineData("../outside.md")]
    [InlineData("docs\\adr\\0001.md")]
    [InlineData("docs/adr/../0001.md")]
    public void RejectsUnsafeOrNonNormalizedDecisionPaths(string decisionPath)
    {
        using var repository = TemporaryImpactRepository.Create();
        repository.WriteManifest(Mapping("ADR-1", decisionPath, "src/**"));

        Assert.Throws<ImpactManifestException>(
            () => ImpactManifestLoader.Load(repository.Root, ".adrguard-impact.json"));
    }

    [Theory]
    [InlineData("../src/**")]
    [InlineData("src\\api\\**")]
    [InlineData("src/**suffix")]
    [InlineData("src/[ab].cs")]
    [InlineData("src//api")]
    public void RejectsUnsafeOrAmbiguousPatterns(string pattern)
    {
        using var repository = TemporaryImpactRepository.Create();
        repository.Write("docs/adr/0001-active.md", Adr("Accepted"));
        repository.WriteManifest(Mapping("ADR-1", "docs/adr/0001-active.md", pattern));

        Assert.Throws<ImpactManifestException>(
            () => ImpactManifestLoader.Load(repository.Root, ".adrguard-impact.json"));
    }

    [Fact]
    public void RejectsDuplicateIdentityConflictingPathsAndDuplicatePatterns()
    {
        using var duplicateIdentity = TemporaryImpactRepository.Create();
        duplicateIdentity.WriteManifest(
            Mapping("ADR-1", "docs/adr/0001-one.md", "src/one/**"),
            Mapping("ADR-1", "docs/adr/0001-two.md", "src/two/**"));
        Assert.Contains(
            "mapped more than once",
            Assert.Throws<ImpactManifestException>(
                () => ImpactManifestLoader.Load(duplicateIdentity.Root, ".adrguard-impact.json")).Message,
            StringComparison.Ordinal);

        using var conflictingPath = TemporaryImpactRepository.Create();
        conflictingPath.WriteManifest(
            Mapping("ADR-1", "docs/adr/decision.md", "src/one/**"),
            Mapping("slug:decision", "docs/adr/decision.md", "src/two/**"));
        Assert.Contains(
            "assigned to both",
            Assert.Throws<ImpactManifestException>(
                () => ImpactManifestLoader.Load(conflictingPath.Root, ".adrguard-impact.json")).Message,
            StringComparison.Ordinal);

        using var duplicatePattern = TemporaryImpactRepository.Create();
        duplicatePattern.Write("docs/adr/0001-active.md", Adr("Accepted"));
        duplicatePattern.WriteManifestRaw(
            "{\"schemaVersion\":\"1.0\",\"mappings\":[{\"decision\":{\"stableId\":\"ADR-1\",\"path\":\"docs/adr/0001-active.md\"},\"patterns\":[\"src/**\",\"src/**\"],\"relationship\":\"governs\",\"reason\":\"Reason\"}]}" );
        Assert.Contains(
            "duplicated",
            Assert.Throws<ImpactManifestException>(
                () => ImpactManifestLoader.Load(duplicatePattern.Root, ".adrguard-impact.json")).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MissingRenamedAndMismatchedTargetsRemainUnknown()
    {
        using var repository = TemporaryImpactRepository.Create();
        repository.Write("docs/adr/0002-renamed.md", Adr("Accepted"));
        repository.WriteManifest(
            Mapping("ADR-1", "docs/adr/0001-missing.md", "src/one/**"),
            Mapping("ADR-99", "docs/adr/0002-renamed.md", "src/two/**"));

        var manifest = ImpactManifestLoader.Load(repository.Root, ".adrguard-impact.json");

        Assert.All(
            manifest.Mappings,
            mapping => Assert.Equal(ImpactDecisionResolutionKind.Unknown, mapping.Resolution.Kind));
        Assert.Contains(
            manifest.Mappings,
            mapping => mapping.Resolution.Detail!.Contains("stale or renamed", StringComparison.Ordinal));
        Assert.Contains(
            manifest.Mappings,
            mapping => mapping.Resolution.Detail!.Contains("not 'ADR-99'", StringComparison.Ordinal));
    }

    [Fact]
    public void ResolvesUnnumberedStableIdentity()
    {
        using var repository = TemporaryImpactRepository.Create();
        repository.Write("docs/adr/use-cache.md", Adr("Accepted"));
        repository.WriteManifest(Mapping("slug:use-cache", "docs/adr/use-cache.md", "src/cache/*"));

        var mapping = Assert.Single(
            ImpactManifestLoader.Load(repository.Root, ".adrguard-impact.json").Mappings);

        Assert.Equal(ImpactDecisionResolutionKind.Active, mapping.Resolution.Kind);
        Assert.Equal("slug:use-cache", mapping.Resolution.Document!.StableId);
    }

    [Fact]
    public void RejectsManifestAndAdrSymlinkEscapesWhenPlatformAllowsLinks()
    {
        using var repository = TemporaryImpactRepository.Create();
        var outside = Path.Combine(Path.GetTempPath(), $"adr-impact-outside-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outside);

        try
        {
            File.WriteAllText(Path.Combine(outside, "manifest.json"), "{}");
            File.WriteAllText(Path.Combine(outside, "0001-outside.md"), Adr("Accepted"));
            var linkedDirectory = Path.Combine(repository.Root, "linked");
            try
            {
                Directory.CreateSymbolicLink(linkedDirectory, outside);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                return;
            }

            Assert.Throws<ImpactManifestException>(
                () => ImpactManifestLoader.Load(repository.Root, "linked/manifest.json"));

            repository.WriteManifest(Mapping("ADR-1", "linked/0001-outside.md", "src/**"));
            Assert.Throws<ImpactManifestException>(
                () => ImpactManifestLoader.Load(repository.Root, ".adrguard-impact.json"));
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    private static string Mapping(
        string stableId,
        string path,
        string pattern,
        string relationship = "governs") =>
        $$"""
        {"decision":{"stableId":"{{stableId}}","path":"{{JsonEscape(path)}}"},"patterns":["{{JsonEscape(pattern)}}"],"relationship":"{{relationship}}","reason":"Review this explicit mapping."}
        """;

    private static string JsonEscape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal);

    private static string Adr(string status) =>
        $$"""
        # Decision

        ## Status

        {{status}}

        ## Context

        Context.

        ## Decision

        Decision.

        ## Consequences

        Consequences.
        """;

    private sealed class TemporaryImpactRepository : IDisposable
    {
        internal string Root { get; } = Path.Combine(
            Path.GetTempPath(),
            $"adr-impact-manifest-{Guid.NewGuid():N}");

        private TemporaryImpactRepository() => Directory.CreateDirectory(Root);

        internal static TemporaryImpactRepository Create() => new();

        internal void Write(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        internal void WriteManifest(params string[] mappings) =>
            WriteManifestRaw(
                $"{{\"schemaVersion\":\"1.0\",\"mappings\":[{string.Join(',', mappings)}]}}");

        internal void WriteManifestRaw(string json) =>
            File.WriteAllText(Path.Combine(Root, ".adrguard-impact.json"), json);

        internal void WriteManifestBytes(byte[] bytes) =>
            File.WriteAllBytes(Path.Combine(Root, ".adrguard-impact.json"), bytes);

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
