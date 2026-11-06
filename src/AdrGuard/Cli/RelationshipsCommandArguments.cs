using AdrGuard.Validation;

namespace AdrGuard.Cli;

internal sealed record RelationshipsCommandArguments(
    string DirectoryPath,
    AdrFormat AdrFormat,
    string? LifecycleStatuses,
    bool ConventionalSupersession,
    string? FilenamePolicy,
    string? ValidationProfile)
{
    internal static bool TryParse(
        IReadOnlyList<string> args,
        out RelationshipsCommandArguments arguments)
    {
        var directory = ".";
        var directoryAssigned = false;
        var format = AdrFormat.Canonical;
        var formatAssigned = false;
        string? lifecycleStatuses = null;
        var conventionalSupersession = false;
        string? filenamePolicy = null;
        string? validationProfile = null;
        var outputFormatAssigned = false;

        var index = 1;
        while (index < args.Count)
        {
            var argument = args[index++];
            if (argument == "--conventional-supersession")
            {
                if (conventionalSupersession) return Fail(out arguments);
                conventionalSupersession = true;
                continue;
            }

            if (argument.StartsWith('-'))
            {
                if (!TryReadOptionValue(args, ref index, out var value)
                    || !TryApplyOption(
                        argument,
                        value,
                        ref format,
                        ref formatAssigned,
                        ref lifecycleStatuses,
                        ref filenamePolicy,
                        ref validationProfile,
                        ref outputFormatAssigned)) return Fail(out arguments);
                continue;
            }

            if (directoryAssigned) return Fail(out arguments);
            directory = argument;
            directoryAssigned = true;
        }

        arguments = new RelationshipsCommandArguments(
            directory,
            format,
            lifecycleStatuses,
            conventionalSupersession,
            filenamePolicy,
            validationProfile);
        return true;
    }

    private static bool TryReadOptionValue(
        IReadOnlyList<string> args,
        ref int index,
        out string value)
    {
        if (index >= args.Count)
        {
            value = string.Empty;
            return false;
        }

        value = args[index++];
        return !string.IsNullOrWhiteSpace(value) && !value.StartsWith('-');
    }

    private static bool TryApplyOption(
        string argument,
        string value,
        ref AdrFormat format,
        ref bool formatAssigned,
        ref string? lifecycleStatuses,
        ref string? filenamePolicy,
        ref string? validationProfile,
        ref bool outputFormatAssigned) => argument switch
    {
        "--format" => TrySetOutputFormat(value, ref outputFormatAssigned),
        "--adr-format" => TrySetAdrFormat(value, ref format, ref formatAssigned),
        "--lifecycle-statuses" => TrySetLifecycle(value, ref lifecycleStatuses),
        "--filename-policy" => TrySetFilenamePolicy(value, ref filenamePolicy),
        "--validation-profile" => TrySetValidationProfile(value, ref validationProfile),
        _ => false,
    };

    private static bool TrySetOutputFormat(string value, ref bool assigned)
    {
        if (assigned || value != "json") return false;
        assigned = true;
        return true;
    }

    private static bool TrySetAdrFormat(string value, ref AdrFormat format, ref bool assigned)
    {
        if (assigned) return false;
        format = value switch
        {
            "canonical" => AdrFormat.Canonical,
            "madr-4" => AdrFormat.Madr4,
            _ => (AdrFormat)(-1),
        };
        if (!Enum.IsDefined(format)) return false;
        assigned = true;
        return true;
    }

    private static bool TrySetLifecycle(string value, ref string? lifecycleStatuses)
    {
        if (lifecycleStatuses is not null || !IsValid(value, AdrLifecyclePolicy.Parse)) return false;
        lifecycleStatuses = value;
        return true;
    }

    private static bool TrySetFilenamePolicy(string value, ref string? filenamePolicy)
    {
        if (filenamePolicy is not null || !IsValid(value, AdrFilenamePolicy.Parse)) return false;
        filenamePolicy = value;
        return true;
    }

    private static bool TrySetValidationProfile(string value, ref string? validationProfile)
    {
        if (validationProfile is not null || !IsValid(value, AdrValidationOptionsFactory.ParseProfile)) return false;
        validationProfile = value;
        return true;
    }

    private static bool IsValid<T>(string value, Func<string, T> parse)
    {
        try
        {
            parse(value);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool Fail(out RelationshipsCommandArguments arguments)
    {
        arguments = new RelationshipsCommandArguments(".", AdrFormat.Canonical, null, false, null, null);
        return false;
    }
}
