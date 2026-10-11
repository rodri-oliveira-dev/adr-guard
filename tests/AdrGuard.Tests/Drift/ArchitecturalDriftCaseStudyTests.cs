using System.Text.Json;
using AdrGuard.Cli;
using Xunit;

namespace AdrGuard.Tests.Drift;

public sealed class ArchitecturalDriftCaseStudyTests
{
    [Fact]
    public void PublicCaseStudyReproducesPassViolationBaselineResolutionAndUnknown()
    {
        var source = Path.Combine(FindRepositoryRoot(), "docs", "examples", "architectural-drift");
        var root = Path.Combine(Path.GetTempPath(), $"adr-guard-drift-case-{Guid.NewGuid():N}");
        CopyDirectory(source, root);
        try
        {
            var pass = Run(root, ".adrguard-fitness.json", "--format", "json");
            Assert.Equal(ExitCodes.Success, pass.Code);
            Assert.Equal("pass", FindingOutcomes(pass.Output).Single());

            File.Copy(Path.Combine(root, "src", "Domain", "Domain.violation.csproj.fixture"), Path.Combine(root, "src", "Domain", "Domain.csproj"), true);
            var violation = Run(root, ".adrguard-fitness.json", "--policy", "enforce", "--format", "json");
            Assert.Equal(ExitCodes.PolicyFailed, violation.Code);
            Assert.Equal("violation", FindingOutcomes(violation.Output).Single());

            var baseline = Run(root, ".adrguard-fitness.json", "--write-baseline", "drift-baseline.json");
            Assert.Equal(ExitCodes.Success, baseline.Code);
            File.WriteAllText(Path.Combine(root, "src", "Domain", "Domain.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
            var resolved = Run(root, ".adrguard-fitness.json", "--baseline", "drift-baseline.json", "--format", "json");
            Assert.Equal(ExitCodes.Success, resolved.Code);
            using (var report = JsonDocument.Parse(resolved.Output))
                Assert.Contains(report.RootElement.GetProperty("findings").EnumerateArray(), x => x.GetProperty("baselineState").GetString() == "resolved");

            File.Copy(Path.Combine(root, "src", "Domain", "Domain.unknown.csproj.fixture"), Path.Combine(root, "src", "Domain", "Domain.csproj"), true);
            Assert.Equal(ExitCodes.OperationalError, Run(root, ".adrguard-fitness.json").Code);
            Assert.Equal(ExitCodes.OperationalError, Run(root, ".adrguard-fitness-with-external.json").Code);
        }
        finally { Directory.Delete(root, true); }
    }

    private static (int Code, string Output, string Error) Run(string root, string rules, params string[] options)
    {
        using var output = new StringWriter(); using var error = new StringWriter();
        var args = new List<string> { "drift", root, "--rules", rules }; args.AddRange(options);
        var code = CliApplication.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }
    private static string[] FindingOutcomes(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("findings").EnumerateArray().Select(x => x.GetProperty("outcome").GetString()!).ToArray();
    }
    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AdrGuard.slnx"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)));
    }
}
