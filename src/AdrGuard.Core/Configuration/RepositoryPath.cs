namespace AdrGuard.Configuration;

internal static class RepositoryPath
{
    internal static string ResolveContained(
        string repositoryRoot,
        string relativePath,
        bool allowMissingLeaf = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("Path must be relative to the repository root.", nameof(relativePath));
        }

        var root = Path.GetFullPath(repositoryRoot);
        var resolved = Path.GetFullPath(relativePath, root);
        var relative = Path.GetRelativePath(root, resolved);

        if (relative == ".."
            || Path.IsPathRooted(relative)
            || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new ArgumentException("Path must stay inside the repository root.", nameof(relativePath));
        }

        RejectReparsePoints(root, resolved, allowMissingLeaf);
        return resolved;
    }

    internal static string NormalizeRelative(string repositoryRoot, string path)
    {
        var resolved = ResolveContained(repositoryRoot, path);
        return Path.GetRelativePath(Path.GetFullPath(repositoryRoot), resolved)
            .Replace(Path.DirectorySeparatorChar, '/');
    }

    private static void RejectReparsePoints(
        string root,
        string resolved,
        bool allowMissingLeaf)
    {
        var current = root;

        if (File.Exists(current) || Directory.Exists(current))
        {
            RejectReparsePoint(current);
        }

        var relative = Path.GetRelativePath(root, resolved);
        foreach (var segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);

            if (!File.Exists(current) && !Directory.Exists(current))
            {
                if (allowMissingLeaf)
                {
                    return;
                }

                throw new FileNotFoundException("Configured path does not exist.", current);
            }

            RejectReparsePoint(current);
        }
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Symbolic links and reparse points are not allowed in managed paths: '{path}'.");
        }
    }
}
