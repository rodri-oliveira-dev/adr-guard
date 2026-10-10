namespace AdrGuard.Git;

internal enum GitChangeScope
{
    Committed,
    Staged,
    Unstaged,
    Untracked,
}

internal enum GitChangeKind
{
    Added,
    Modified,
    Deleted,
    Renamed,
    Copied,
    TypeChanged,
    Unmerged,
}

internal sealed record GitChangedPath(
    GitChangeScope Scope,
    GitChangeKind Kind,
    string? OldPath,
    string? NewPath,
    int? Similarity);

internal sealed record GitChangeInventory(
    string RepositoryRoot,
    string BaseReference,
    string MergeBase,
    IReadOnlyList<GitChangedPath> Changes);

internal static class GitChangeInventoryReader
{
    internal const int MaximumChanges = 10_000;
    internal const int MaximumGitOutputCharacters = 4_194_304;
    internal static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(20);

    internal static GitChangeInventory Read(
        string repositoryPath,
        string baseReference,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        GitChangeDetector.ValidateReference(baseReference);

        var workingDirectory = Path.GetFullPath(repositoryPath);
        if (!Directory.Exists(workingDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Git repository path does not exist: '{workingDirectory}'.");
        }

        var repositoryRoot = Run(
            workingDirectory,
            ["rev-parse", "--show-toplevel"],
            cancellationToken).Trim();
        if (repositoryRoot.Length == 0)
        {
            throw new GitOperationException("Git did not return a repository root.");
        }

        repositoryRoot = Path.GetFullPath(repositoryRoot);
        EnsureContained(repositoryRoot, workingDirectory);

        var verifiedBase = Run(
            repositoryRoot,
            ["rev-parse", "--verify", "--end-of-options", $"{baseReference}^{{commit}}"],
            cancellationToken).Trim();
        var mergeBase = Run(
            repositoryRoot,
            ["merge-base", verifiedBase, "HEAD"],
            cancellationToken).Trim();
        if (mergeBase.Length == 0)
        {
            throw new GitOperationException(
                $"No merge base is available for '{baseReference}'. Fetch sufficient history and retry.");
        }

        var changes = new List<GitChangedPath>();
        AddNameStatus(
            changes,
            Run(
                repositoryRoot,
                ["diff", "--name-status", "-z", "--find-renames", "--find-copies", "--find-copies-harder", mergeBase, "HEAD"],
                cancellationToken),
            GitChangeScope.Committed);
        AddNameStatus(
            changes,
            Run(
                repositoryRoot,
                ["diff", "--cached", "--name-status", "-z", "--find-renames", "--find-copies", "--find-copies-harder", "HEAD"],
                cancellationToken),
            GitChangeScope.Staged);
        AddNameStatus(
            changes,
            Run(
                repositoryRoot,
                ["diff", "--name-status", "-z", "--find-renames", "--find-copies", "--find-copies-harder"],
                cancellationToken),
            GitChangeScope.Unstaged);

        foreach (var path in SplitNull(
                     Run(
                         repositoryRoot,
                         ["ls-files", "--others", "--exclude-standard", "-z"],
                         cancellationToken)))
        {
            AddBounded(
                changes,
                new GitChangedPath(
                    GitChangeScope.Untracked,
                    GitChangeKind.Added,
                    null,
                    NormalizeGitPath(repositoryRoot, path),
                    null));
        }

        var ordered = changes
            .OrderBy(change => change.NewPath ?? change.OldPath, StringComparer.Ordinal)
            .ThenBy(change => change.OldPath, StringComparer.Ordinal)
            .ThenBy(change => change.Scope)
            .ThenBy(change => change.Kind)
            .ThenBy(change => change.Similarity)
            .ToArray();

        return new GitChangeInventory(
            repositoryRoot,
            baseReference,
            mergeBase,
            Array.AsReadOnly(ordered));
    }

    internal static IReadOnlyList<GitChangedPath> ParseNameStatus(
        string output,
        GitChangeScope scope)
    {
        var changes = new List<GitChangedPath>();
        AddNameStatus(changes, output, scope);
        return changes;
    }

    private static void AddNameStatus(
        List<GitChangedPath> changes,
        string output,
        GitChangeScope scope)
    {
        var tokens = SplitNull(output);
        for (var index = 0; index < tokens.Length;)
        {
            var status = tokens[index++];
            if (status.Length == 0 || index >= tokens.Length)
            {
                throw new GitOperationException("Git returned an invalid name-status record.");
            }

            var code = status[0];
            var similarity = ParseSimilarity(status);
            if (code is 'R' or 'C')
            {
                if (index + 1 >= tokens.Length)
                {
                    throw new GitOperationException("Git returned an incomplete rename/copy record.");
                }

                AddBounded(
                    changes,
                    new GitChangedPath(
                        scope,
                        code == 'R' ? GitChangeKind.Renamed : GitChangeKind.Copied,
                        ValidateRawPath(tokens[index++]),
                        ValidateRawPath(tokens[index++]),
                        similarity));
                continue;
            }

            var path = ValidateRawPath(tokens[index++]);
            var kind = code switch
            {
                'A' => GitChangeKind.Added,
                'M' => GitChangeKind.Modified,
                'D' => GitChangeKind.Deleted,
                'T' => GitChangeKind.TypeChanged,
                'U' => GitChangeKind.Unmerged,
                _ => throw new GitOperationException(
                    $"Git returned unsupported change status '{status}'."),
            };
            AddBounded(
                changes,
                new GitChangedPath(
                    scope,
                    kind,
                    kind == GitChangeKind.Deleted ? path : null,
                    kind == GitChangeKind.Deleted ? null : path,
                    null));
        }
    }

    private static int? ParseSimilarity(string status)
    {
        if (status[0] is not ('R' or 'C'))
        {
            if (status.Length != 1)
            {
                throw new GitOperationException(
                    $"Git returned invalid change status '{status}'.");
            }

            return null;
        }

        return status.Length > 1
            && int.TryParse(
                status.AsSpan(1),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var similarity)
            && similarity is >= 0 and <= 100
                ? similarity
                : throw new GitOperationException(
                    $"Git returned invalid similarity status '{status}'.");
    }

    private static string NormalizeGitPath(string repositoryRoot, string path)
    {
        var validated = ValidateRawPath(path);
        var fullPath = Path.GetFullPath(
            validated.Replace('/', Path.DirectorySeparatorChar),
            repositoryRoot);
        EnsureContained(repositoryRoot, fullPath);
        return Path.GetRelativePath(repositoryRoot, fullPath)
            .Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string ValidateRawPath(string path)
    {
        if (string.IsNullOrEmpty(path)
            || Path.IsPathRooted(path)
            || path.Contains('\\')
            || path.Contains('\0'))
        {
            throw new GitOperationException("Git returned an unsafe path.");
        }

        var segments = path.Split('/');
        if (segments.Any(segment => segment is "" or "." or ".."))
        {
            throw new GitOperationException("Git returned an unsafe path segment.");
        }

        return path;
    }

    private static void AddBounded(
        List<GitChangedPath> changes,
        GitChangedPath change)
    {
        if (changes.Count >= MaximumChanges)
        {
            throw new GitOperationException(
                $"Git change inventory exceeds the {MaximumChanges}-entry limit.");
        }

        changes.Add(change);
    }

    private static string Run(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) =>
        GitCommandRunner.RunBounded(
            workingDirectory,
            arguments,
            CommandTimeout,
            MaximumGitOutputCharacters,
            cancellationToken);

    private static string[] SplitNull(string value) =>
        value.Split('\0', StringSplitOptions.RemoveEmptyEntries);

    private static void EnsureContained(string root, string path)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path));
        if (relative == ".."
            || Path.IsPathRooted(relative)
            || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new GitOperationException(
                $"Path '{path}' is outside repository scope '{root}'.");
        }
    }
}
