namespace AdrGuard.Git;

internal sealed record GitChangeSet(
    string RepositoryRoot,
    IReadOnlySet<string> CurrentPaths,
    IReadOnlySet<string> RemovedPaths);

internal static class GitChangeDetector
{
    internal static GitChangeSet Detect(
        string checkedDirectory,
        string baseReference,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkedDirectory);
        ValidateReference(baseReference);

        var workingDirectory = Path.GetFullPath(checkedDirectory);
        var repositoryRoot = GitCommandRunner.Run(
            workingDirectory,
            ["rev-parse", "--show-toplevel"],
            cancellationToken).Trim();
        if (repositoryRoot.Length == 0)
        {
            throw new GitOperationException("Git did not return a repository root.");
        }

        repositoryRoot = Path.GetFullPath(repositoryRoot);
        EnsureContained(repositoryRoot, workingDirectory);

        var verifiedBase = GitCommandRunner.Run(
            repositoryRoot,
            ["rev-parse", "--verify", "--end-of-options", $"{baseReference}^{{commit}}"],
            cancellationToken).Trim();
        var mergeBase = GitCommandRunner.Run(
            repositoryRoot,
            ["merge-base", verifiedBase, "HEAD"],
            cancellationToken).Trim();
        if (mergeBase.Length == 0)
        {
            throw new GitOperationException(
                $"No merge base is available for '{baseReference}'. Fetch sufficient history and retry.");
        }

        var directoryRelative = Path.GetRelativePath(repositoryRoot, workingDirectory)
            .Replace(Path.DirectorySeparatorChar, '/');
        if (directoryRelative == ".")
        {
            directoryRelative = string.Empty;
        }

        var current = new HashSet<string>(StringComparer.Ordinal);
        var removed = new HashSet<string>(StringComparer.Ordinal);

        AddDiff(
            GitCommandRunner.Run(
                repositoryRoot,
                BuildPathCommand(
                    ["diff", "--name-status", "-z", "--find-renames", mergeBase, "HEAD"],
                    directoryRelative),
                cancellationToken),
            repositoryRoot,
            workingDirectory,
            current,
            removed);
        AddDiff(
            GitCommandRunner.Run(
                repositoryRoot,
                BuildPathCommand(
                    ["diff", "--name-status", "-z", "--find-renames", "HEAD"],
                    directoryRelative),
                cancellationToken),
            repositoryRoot,
            workingDirectory,
            current,
            removed);

        var untracked = GitCommandRunner.Run(
            repositoryRoot,
            BuildPathCommand(
                ["ls-files", "--others", "--exclude-standard", "-z"],
                directoryRelative),
            cancellationToken);
        foreach (var path in SplitNull(untracked))
        {
            var fullPath = ResolveGitPath(repositoryRoot, path);
            if (IsAdrPath(workingDirectory, fullPath))
            {
                current.Add(fullPath);
            }
        }

        return new GitChangeSet(repositoryRoot, current, removed);
    }

    private static string[] BuildPathCommand(string[] prefix, string path)
    {
        if (path.Length == 0)
        {
            return prefix;
        }

        return [.. prefix, "--", path];
    }

    private static void AddDiff(
        string output,
        string repositoryRoot,
        string checkedDirectory,
        HashSet<string> current,
        HashSet<string> removed)
    {
        var tokens = SplitNull(output);
        for (var index = 0; index < tokens.Length;)
        {
            var status = tokens[index++];
            if (status.Length == 0 || index >= tokens.Length)
            {
                throw new GitOperationException("Git returned an invalid name-status record.");
            }

            if (status[0] is 'R' or 'C')
            {
                if (index + 1 >= tokens.Length)
                {
                    throw new GitOperationException("Git returned an incomplete rename/copy record.");
                }

                var oldPath = ResolveGitPath(repositoryRoot, tokens[index++]);
                var newPath = ResolveGitPath(repositoryRoot, tokens[index++]);
                if (IsAdrPath(checkedDirectory, oldPath))
                {
                    removed.Add(oldPath);
                }

                if (IsAdrPath(checkedDirectory, newPath))
                {
                    current.Add(newPath);
                }

                continue;
            }

            var path = ResolveGitPath(repositoryRoot, tokens[index++]);
            if (!IsAdrPath(checkedDirectory, path))
            {
                continue;
            }

            if (status[0] == 'D')
            {
                removed.Add(path);
            }
            else
            {
                current.Add(path);
            }
        }
    }

    private static string[] SplitNull(string value) =>
        value.Split('\0', StringSplitOptions.RemoveEmptyEntries);

    private static string ResolveGitPath(string repositoryRoot, string path)
    {
        if (Path.IsPathRooted(path) || path.Contains('\0'))
        {
            throw new GitOperationException("Git returned an unsafe path.");
        }

        var fullPath = Path.GetFullPath(path, repositoryRoot);
        EnsureContained(repositoryRoot, fullPath);
        return fullPath;
    }

    private static bool IsAdrPath(string checkedDirectory, string path)
    {
        try
        {
            EnsureContained(checkedDirectory, path);
        }
        catch (GitOperationException)
        {
            return false;
        }

        return string.Equals(Path.GetExtension(path), ".md", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Path.GetFileName(path), "README.md", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureContained(string root, string path)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path));
        if (relative == ".."
            || Path.IsPathRooted(relative)
            || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new GitOperationException($"Path '{path}' is outside repository scope '{root}'.");
        }
    }

    internal static void ValidateReference(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        if (reference.Length > 256
            || reference[0] == '-'
            || reference.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)))
        {
            throw new ArgumentException("Base reference is invalid or unsafe.", nameof(reference));
        }
    }
}
