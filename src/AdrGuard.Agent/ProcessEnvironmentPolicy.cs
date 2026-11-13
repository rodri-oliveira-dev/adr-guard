using System.Diagnostics;

namespace AdrGuard.Agent;

internal static class ProcessEnvironmentPolicy
{
    private static readonly string[] AllowedVariables =
    [
        "DOTNET_ROOT",
        "DOTNET_ROOT_X64",
        "HOME",
        "LANG",
        "LC_ALL",
        "PATH",
        "PATHEXT",
        "SystemRoot",
        "TEMP",
        "TMP",
        "TMPDIR",
        "USERPROFILE",
        "WINDIR",
    ];

    internal static void Apply(
        ProcessStartInfo startInfo,
        Func<string, string?>? getEnvironmentVariable = null)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;

        startInfo.Environment.Clear();
        foreach (var name in AllowedVariables)
        {
            var value = getEnvironmentVariable(name);
            if (!string.IsNullOrEmpty(value))
            {
                startInfo.Environment[name] = value;
            }
        }
    }
}
