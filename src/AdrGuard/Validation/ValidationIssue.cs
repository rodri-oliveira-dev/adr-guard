namespace AdrGuard.Validation;

internal enum ValidationSeverity { Warning, Error }

internal sealed record ValidationIssue(
    string Code,
    string FilePath,
    string Message,
    ValidationSeverity Severity = ValidationSeverity.Error);
