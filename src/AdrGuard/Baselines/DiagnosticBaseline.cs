using AdrGuard.Validation;

namespace AdrGuard.Baselines;

internal sealed record DiagnosticBaseline(
    string SchemaVersion,
    IReadOnlyList<DiagnosticBaselineEntry> Diagnostics);

internal sealed record DiagnosticBaselineEntry(
    string Fingerprint,
    string Code,
    string File,
    string Message);

internal sealed record DiagnosticBaselineComparison(
    IReadOnlyList<ValidationIssue> NewIssues,
    IReadOnlyList<ValidationIssue> ExistingIssues,
    IReadOnlyList<DiagnosticBaselineEntry> ResolvedEntries,
    IReadOnlyDictionary<string, string> StateByFingerprint);
