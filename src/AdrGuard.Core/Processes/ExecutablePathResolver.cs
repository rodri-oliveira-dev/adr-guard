namespace AdrGuard.Processes;

internal static class ExecutablePathResolver
{
    internal static string Resolve(string executableName)
        => Resolve(executableName, Environment.GetEnvironmentVariable("PATH"));

    internal static string Resolve(string executableName, string? pathValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executableName);
        if (!string.Equals(executableName, Path.GetFileName(executableName), StringComparison.Ordinal)
            || executableName.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
        {
            throw new ArgumentException("Executable name must not contain a path.", nameof(executableName));
        }

        var fileName = OperatingSystem.IsWindows()
            && !executableName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? $"{executableName}.exe"
                : executableName;
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            throw new FileNotFoundException($"Unable to locate the '{executableName}' executable because PATH is empty.");
        }

        foreach (var entry in pathValue.Split(Path.PathSeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Path.IsPathFullyQualified(entry))
            {
                continue;
            }

            var candidate = Path.GetFullPath(Path.Combine(entry, fileName));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Unable to locate the '{executableName}' executable on PATH.");
    }
}
