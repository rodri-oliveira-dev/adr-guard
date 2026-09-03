namespace AdrGuard.Validation;

internal sealed record ValidationResult(IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Issues.Count == 0;
}
