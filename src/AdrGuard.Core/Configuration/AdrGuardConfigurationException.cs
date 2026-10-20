namespace AdrGuard.Configuration;

internal sealed class AdrGuardConfigurationException : Exception
{
    internal AdrGuardConfigurationException(string message)
        : base(message)
    {
    }

    internal AdrGuardConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
