namespace AdrGuard.Validation;

internal enum AdrValidationProfile { Legacy, Advisory, Standard, Strict }

internal static class AdrValidationOptionsFactory
{
    internal static AdrValidationOptions Create(
        AdrFormat format,
        string? repositoryRoot,
        string? lifecycleStatuses,
        bool conventionalSupersession,
        string? filenamePolicy,
        string? placeholderPolicy,
        bool validateMetadata,
        string? profileName)
    {
        var profile = ParseProfile(profileName);
        var profileLifecycle = profile is AdrValidationProfile.Standard or AdrValidationProfile.Strict
            && !(lifecycleStatuses?.Contains("Rejected=", StringComparison.OrdinalIgnoreCase) ?? false)
                ? string.IsNullOrWhiteSpace(lifecycleStatuses) ? "Rejected=rejected" : $"{lifecycleStatuses},Rejected=rejected"
                : lifecycleStatuses;
        var effectivePlaceholder = placeholderPolicy ?? profile switch
        {
            AdrValidationProfile.Advisory or AdrValidationProfile.Standard => "warn",
            AdrValidationProfile.Strict => "error",
            _ => "off",
        };

        return new AdrValidationOptions(
            format,
            repositoryRoot,
            AdrLifecyclePolicy.Parse(profileLifecycle),
            conventionalSupersession || profile is AdrValidationProfile.Standard or AdrValidationProfile.Strict,
            AdrFilenamePolicy.Parse(filenamePolicy),
            effectivePlaceholder switch
            {
                "warn" => PlaceholderPolicy.Warn,
                "error" => PlaceholderPolicy.Error,
                _ => PlaceholderPolicy.Off,
            },
            validateMetadata || profile is AdrValidationProfile.Standard or AdrValidationProfile.Strict);
    }

    internal static AdrValidationProfile ParseProfile(string? value) => value switch
    {
        null or "legacy" => AdrValidationProfile.Legacy,
        "advisory" => AdrValidationProfile.Advisory,
        "standard" => AdrValidationProfile.Standard,
        "strict" => AdrValidationProfile.Strict,
        _ => throw new ArgumentException("Validation profile must be legacy, advisory, standard, or strict.", nameof(value)),
    };
}
