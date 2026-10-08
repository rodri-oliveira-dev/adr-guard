namespace AdrGuard.Git;

internal sealed class GitOperationException : Exception
{
    internal GitOperationException(string message)
        : base(message)
    {
    }
}
