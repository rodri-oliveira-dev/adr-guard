using System.Text.Json;
using AdrGuard.Cli;
using Xunit;

namespace AdrGuard.Tests.Cli;

public sealed class DriftCommandIntegrationTests
{
    [Fact]
    public void PassJsonIsStableAndKeepsStderrClean()
    {
        using var repository = Repository.Create(violation: false);
        var first = Run(repository.Root, "drift", repository.Root, "--rules", ".adrguard-fitness.json", "--format", "json");
        var second = Run(repository.Root, "drift", repository.Root, "--rules", ".adrguard-fitness.json", "--format", "json");
        Assert.Equal(ExitCodes.Success, first.Code);
        Assert.Equal(string.Empty, first.Error);
        Assert.Equal(first.Output, second.Output);
        using var json = JsonDocument.Parse(first.Output);
        Assert.Equal("pass", json.RootElement.GetProperty("findings")[0].GetProperty("outcome").GetString());
    }

    [Fact]
    public void ViolationIsAdvisoryByDefaultAndEnforcedOnlyWhenExplicit()
    {
        using var repository = Repository.Create(violation: true);
        var advisory = Run(repository.Root, "drift", repository.Root, "--rules", ".adrguard-fitness.json");
        Assert.Equal(ExitCodes.Success, advisory.Code);
        Assert.Contains("[violation/new]", advisory.Output, StringComparison.Ordinal);

        var enforced = Run(repository.Root, "drift", repository.Root, "--rules", ".adrguard-fitness.json", "--policy", "enforce");
        Assert.Equal(ExitCodes.PolicyFailed, enforced.Code);
        Assert.Contains("enforcement failed", enforced.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitBaselineMakesExistingViolationVisibleButNonBlocking()
    {
        using var repository = Repository.Create(violation: true);
        var create = Run(repository.Root, "drift", repository.Root, "--rules", ".adrguard-fitness.json", "--write-baseline", "drift-baseline.json");
        Assert.Equal(ExitCodes.Success, create.Code);
        Assert.True(File.Exists(Path.Combine(repository.Root, "drift-baseline.json")));

        var result = Run(repository.Root, "drift", repository.Root, "--rules", ".adrguard-fitness.json", "--baseline", "drift-baseline.json", "--policy", "enforce");
        Assert.Equal(ExitCodes.Success, result.Code);
        Assert.Contains("[violation/existing]", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownEvidenceReturnsOperationalExitAndNeverGreenText()
    {
        using var repository = Repository.Create(violation: false);
        repository.WriteDomain("<Project><ItemGroup><ProjectReference Include=\"$(Dynamic)\" /></ItemGroup></Project>");
        var result = Run(repository.Root, "drift", repository.Root, "--rules", ".adrguard-fitness.json", "--policy", "enforce");
        Assert.Equal(ExitCodes.OperationalError, result.Code);
        Assert.Contains("[unknown/current]", result.Output, StringComparison.Ordinal);
        Assert.Contains("inconclusive", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void BaselineWithoutFingerprintReturnsControlledOperationalError()
    {
        using var repository = Repository.Create(violation: false);
        File.WriteAllText(Path.Combine(repository.Root, "bad-baseline.json"), "{\"schemaVersion\":\"1.0\",\"repository\":{\"rulesManifest\":\".adrguard-fitness.json\",\"commit\":null},\"createdAt\":\"2026-01-01T00:00:00Z\",\"findings\":[{\"ruleId\":\"domain-no-infrastructure\",\"adrStableId\":\"ADR-1\",\"outcome\":\"violation\",\"checkType\":\"forbid-project-reference\",\"from\":null,\"to\":null,\"confidence\":\"deterministic-static\"}]}");
        var result = Run(repository.Root, "drift", repository.Root, "--rules", ".adrguard-fitness.json", "--baseline", "bad-baseline.json");
        Assert.Equal(ExitCodes.OperationalError, result.Code);
        Assert.Equal(string.Empty, result.Output);
        Assert.Contains("Unable to analyze architecture drift", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--policy", "unsafe")]
    [InlineData("--format", "sarif")]
    [InlineData("--update-baseline", "")]
    public void InvalidOptionsReturnUsageError(string option, string value)
    {
        var arguments = value.Length == 0
            ? new[] { "drift", ".", "--rules", "rules.json", option }
            : new[] { "drift", ".", "--rules", "rules.json", option, value };
        var result = Run(Directory.GetCurrentDirectory(), arguments);
        Assert.Equal(ExitCodes.UsageError, result.Code);
        Assert.Equal(string.Empty, result.Output);
    }

    private static Result Run(string workingDirectory, params string[] args)
    {
        _ = workingDirectory;
        using var output = new StringWriter(); using var error = new StringWriter();
        var code = CliApplication.Run(args, output, error);
        return new(code, output.ToString(), error.ToString());
    }
    private sealed record Result(int Code, string Output, string Error);

    private sealed class Repository : IDisposable
    {
        private Repository(string root) => Root = root;
        internal string Root { get; }
        internal static Repository Create(bool violation)
        {
            var root = Path.Combine(Path.GetTempPath(), $"adr-guard-drift-cli-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(root, "docs", "adr")); Directory.CreateDirectory(Path.Combine(root, "src", "Domain")); Directory.CreateDirectory(Path.Combine(root, "src", "Infrastructure"));
            File.WriteAllText(Path.Combine(root, "docs", "adr", "0001-boundary.md"), "# Boundary\n\n## Status\nAccepted\n\n## Context\nContext.\n\n## Decision\nDecision.\n\n## Consequences\nConsequences.\n");
            File.WriteAllText(Path.Combine(root, "src", "Infrastructure", "Infrastructure.csproj"), "<Project />");
            File.WriteAllText(Path.Combine(root, ".adrguard-fitness.json"), Manifest);
            var repository = new Repository(root); repository.WriteDomain(violation ? "<Project><ItemGroup><ProjectReference Include=\"../Infrastructure/Infrastructure.csproj\" /></ItemGroup></Project>" : "<Project />"); return repository;
        }
        internal void WriteDomain(string xml) => File.WriteAllText(Path.Combine(Root, "src", "Domain", "Domain.csproj"), xml);
        public void Dispose() => Directory.Delete(Root, true);
        private const string Manifest = """
            {"schemaVersion":"1.0","rules":[{"id":"domain-no-infrastructure","decision":{"stableId":"ADR-1","path":"docs/adr/0001-boundary.md"},"scope":{"projects":["src/Domain/Domain.csproj","src/Infrastructure/Infrastructure.csproj"]},"check":{"type":"forbid-project-reference","from":"src/Domain/Domain.csproj","to":"src/Infrastructure/Infrastructure.csproj"},"evidence":{"source":"dotnet-project-reference"},"failure":{"severity":"error","onInconclusive":"unknown"},"gate":true}]}
            """;
    }
}
