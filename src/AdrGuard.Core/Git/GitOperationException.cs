namespace AdrGuard.Git;

public sealed class GitOperationException : Exception
{
    internal GitOperationException(string message)
        : base(message)
    {
    }
}
