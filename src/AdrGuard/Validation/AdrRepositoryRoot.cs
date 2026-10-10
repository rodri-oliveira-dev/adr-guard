using AdrGuard.Configuration;

namespace AdrGuard.Validation;

/// <summary>
/// Resolves the boundary used to validate local Markdown links from the checked
/// directory, not from an unrelated process working directory.
/// </summary>
internal static class AdrRepositoryRoot
{
    internal static string Resolve(string directoryPath)
    {
        var checkedDirectory = Path.GetFullPath(directoryPath);

        // An explicit ADR directory may be selected from outside the repository
        // or from a nested working directory. Prefer the nearest repository marker.
        for (var current = new DirectoryInfo(checkedDirectory);
             current is not null;
             current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, AdrGuardConfigurationLoader.FileName))
                || Directory.Exists(Path.Combine(current.FullName, ".git"))
                || File.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }
        }

        // Preserve existing invocations from a repository without marker files.
        // An unrelated invocation directory must never widen the trust boundary.
        var invocationRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
        var relative = Path.GetRelativePath(invocationRoot, checkedDirectory);
        var isWithinInvocation = !Path.IsPathRooted(relative)
            && relative != ".."
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
        return isWithinInvocation ? invocationRoot : checkedDirectory;
    }
}
