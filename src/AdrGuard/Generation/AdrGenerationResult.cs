namespace AdrGuard.Generation;

internal sealed record AdrGenerationResult(
    string? Context,
    string? Decision,
    string? Consequences);
