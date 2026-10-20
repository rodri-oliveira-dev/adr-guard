namespace AdrGuard.Git;

/// <summary>
/// Resolves existing Git working directories to one physical identity before
/// comparing them with Git's top-level path. On macOS, /var and /private/var
/// can name the same directory even though Path.GetFullPath preserves both.
/// </summary>
internal static class GitDirectoryIdentity
{
    internal static string Resolve(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        var fullPath = Path.GetFullPath(directoryPath);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Git directory does not exist: '{fullPath}'.");
        }

        return ResolveExisting(fullPath, remainingLinks: 40);
    }

    private static string ResolveExisting(string fullPath, int remainingLinks)
    {
        var root = Path.GetPathRoot(fullPath)
            ?? throw new IOException($"Unable to identify filesystem root for '{fullPath}'.");
        var current = root;
        var relative = Path.GetRelativePath(root, fullPath);
        if (relative != ".")
        {
            foreach (var segment in relative.Split(
                         [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                         StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                var directory = new DirectoryInfo(current);
                if (!directory.Exists)
                {
                    throw new DirectoryNotFoundException($"Git directory does not exist: '{current}'.");
                }

                var target = directory.ResolveLinkTarget(returnFinalTarget: true);
                if (target is not null)
                {
                    if (remainingLinks == 0)
                    {
                        throw new IOException("Too many symbolic links while resolving Git directory.");
                    }

                    // Resolve the target's ancestors too: a link can point to
                    // an alias such as /var that itself resolves to /private/var.
                    current = ResolveExisting(Path.GetFullPath(target.FullName), remainingLinks - 1);
                }
            }
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(current));
    }
}
