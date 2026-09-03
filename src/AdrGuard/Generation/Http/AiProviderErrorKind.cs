namespace AdrGuard.Generation.Http;

internal enum AiProviderErrorKind
{
    Authentication,
    RateLimited,
    ServiceUnavailable,
    UnexpectedResponse,
    InvalidResponse,
    Refused,
    Timeout,
    Network,
}
