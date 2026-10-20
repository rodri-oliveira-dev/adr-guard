namespace AdrGuard.Git;

internal sealed record GitHistoricalFile(
    string Reference,
    string FileName,
    string Content);

internal static class GitHistoricalFileReader
{
    internal static GitHistoricalFile Read(
        string currentPath,
        string reference,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentPath);
        GitChangeDetector.ValidateReference(reference);

        var fullPath = Path.GetFullPath(currentPath);
        var directory = Path.GetDirectoryName(fullPath)
            ?? Directory.GetCurrentDirectory();
        var repositoryRoot = Path.GetFullPath(
            GitCommandRunner.Run(
                directory,
                ["rev-parse", "--show-toplevel"],
                cancellationToken).Trim());
        var relative = Path.GetRelativePath(repositoryRoot, fullPath)
            .Replace(Path.DirectorySeparatorChar, '/');
        if (relative == ".."
            || Path.IsPathRooted(relative)
            || relative.StartsWith("../", StringComparison.Ordinal))
        {
            throw new GitOperationException("Review target is outside the Git repository.");
        }

        var commit = GitCommandRunner.Run(
            repositoryRoot,
            ["rev-parse", "--verify", "--end-of-options", $"{reference}^{{commit}}"],
            cancellationToken).Trim();
        var content = GitCommandRunner.Run(
            repositoryRoot,
            ["show", $"{commit}:{relative}"],
            cancellationToken);
        if (content.Contains('\0'))
        {
            throw new InvalidDataException("Historical ADR contains binary NUL data.");
        }

        return new GitHistoricalFile(
            reference,
            Path.GetFileName(fullPath),
            content);
    }
}
