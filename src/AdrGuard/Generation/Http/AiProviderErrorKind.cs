namespace AdrGuard.Generation.Http;

internal enum AiProviderErrorKind
{
    Authentication,
    RateLimited,
    ServiceUnavailable,
    UnexpectedResponse,
    InvalidResponse,
    Timeout,
    Network,
}
