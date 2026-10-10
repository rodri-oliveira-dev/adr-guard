namespace AdrGuard.Validation;

internal sealed record ValidationResult(IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Issues.All(issue => issue.Severity != ValidationSeverity.Error);
}
