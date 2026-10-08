namespace AdrGuard.Validation;

internal static class IncrementalValidation
{
    private static readonly HashSet<string> GlobalCodes =
    [
        ValidationCodes.DuplicateId,
        ValidationCodes.BrokenReference,
        ValidationCodes.MissingSupersededBy,
        ValidationCodes.SupersessionCycle,
        ValidationCodes.SelfSupersession,
        ValidationCodes.MultipleSuperseders,
        ValidationCodes.InconsistentSupersession,
        ValidationCodes.InactiveDependency,
    ];

    internal static bool IsGlobalCode(string code) => GlobalCodes.Contains(code);

    internal static ValidationResult Select(
        ValidationResult fullResult,
        IReadOnlySet<string> changedPaths)
    {
        ArgumentNullException.ThrowIfNull(fullResult);
        ArgumentNullException.ThrowIfNull(changedPaths);

        var normalized = changedPaths
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.Ordinal);
        return new ValidationResult(fullResult.Issues
            .Where(issue =>
                GlobalCodes.Contains(issue.Code)
                || normalized.Contains(Path.GetFullPath(issue.FilePath)))
            .ToArray());
    }
}
