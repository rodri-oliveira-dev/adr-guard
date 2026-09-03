using AdrGuard.Validation;

namespace AdrGuard.Generation;

internal sealed record AdrGenerationOutcome(
    string? FilePath,
    string? Content,
    ValidationResult ValidationResult,
    bool Written);
