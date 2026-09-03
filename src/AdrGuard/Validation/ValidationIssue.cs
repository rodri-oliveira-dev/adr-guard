namespace AdrGuard.Validation;

internal sealed record ValidationIssue(
    string Code,
    string FilePath,
    string Message);
