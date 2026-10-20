namespace AdrGuard.Configuration;

internal sealed record AdrGuardConfiguration(
    string RootDirectory,
    string AdrDirectory,
    string? Template,
    string? TemplateFile,
    string? AdrFormat,
    string? LifecycleStatuses = null,
    bool ConventionalSupersession = false,
    string? FilenamePolicy = null,
    string? PlaceholderPolicy = null,
    bool ValidateMetadata = false,
    string? ValidationProfile = null)
{
    internal string AdrDirectoryPath =>
        RepositoryPath.ResolveContained(RootDirectory, AdrDirectory);

    internal string? TemplateFilePath => TemplateFile is null
        ? null
        : RepositoryPath.ResolveContained(
            RootDirectory,
            TemplateFile,
            allowMissingLeaf: false);
}
