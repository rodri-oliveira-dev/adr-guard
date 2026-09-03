using System.Net;

namespace AdrGuard.Generation.Http;

internal sealed class AiProviderException : InvalidOperationException
{
    internal AiProviderException(
        AiProviderErrorKind errorKind,
        string message,
        HttpStatusCode? statusCode = null)
        : base(message)
    {
        ErrorKind = errorKind;
        StatusCode = statusCode;
    }

    internal AiProviderErrorKind ErrorKind { get; }

    internal HttpStatusCode? StatusCode { get; }
}
