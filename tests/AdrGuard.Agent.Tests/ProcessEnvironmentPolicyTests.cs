using System.Diagnostics;
using Xunit;

namespace AdrGuard.Agent.Tests;

public sealed class ProcessEnvironmentPolicyTests
{
    [Fact]
    public void CliChildEnvironmentUsesAnAllowlistAndExcludesProviderCredentials()
    {
        var source = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PATH"] = "controlled-path",
            ["TEMP"] = "controlled-temp",
            ["DOTNET_ROOT_ARM64"] = "controlled-arm64-runtime",
            ["DOTNET_ROOT_X86"] = "controlled-x86-runtime",
            ["OPENAI_API_KEY"] = "SECRET_SENTINEL_MUST_NOT_LEAK",
            ["UNRELATED_SECRET"] = "ANOTHER_SECRET_SENTINEL",
        };
        var startInfo = new ProcessStartInfo("fake-cli");
        startInfo.Environment["PREEXISTING_SECRET"] = "PREEXISTING_SECRET_SENTINEL";

        ProcessEnvironmentPolicy.Apply(
            startInfo,
            name => source.GetValueOrDefault(name));

        Assert.Equal("controlled-path", startInfo.Environment["PATH"]);
        Assert.Equal("controlled-temp", startInfo.Environment["TEMP"]);
        Assert.Equal("controlled-arm64-runtime", startInfo.Environment["DOTNET_ROOT_ARM64"]);
        Assert.Equal("controlled-x86-runtime", startInfo.Environment["DOTNET_ROOT_X86"]);
        Assert.DoesNotContain("OPENAI_API_KEY", startInfo.Environment.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("UNRELATED_SECRET", startInfo.Environment.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("PREEXISTING_SECRET", startInfo.Environment.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            startInfo.Environment.Values,
            value => value?.Contains("SECRET_SENTINEL", StringComparison.Ordinal) == true);
    }
}
