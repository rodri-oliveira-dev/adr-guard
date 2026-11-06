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

        for (var index = 1; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument == "--conventional-supersession")
            {
                if (conventionalSupersession) return Fail(out arguments);
                conventionalSupersession = true;
                continue;
            }

            if (argument.StartsWith('-'))
            {
                if (index + 1 >= args.Count) return Fail(out arguments);
                var value = args[++index];
                if (string.IsNullOrWhiteSpace(value) || value.StartsWith('-')) return Fail(out arguments);
                switch (argument)
                {
                    case "--format" when !outputFormatAssigned && value == "json":
                        outputFormatAssigned = true;
                        break;
                    case "--adr-format" when !formatAssigned:
                        format = value switch
                        {
                            "canonical" => AdrFormat.Canonical,
                            "madr-4" => AdrFormat.Madr4,
                            _ => (AdrFormat)(-1),
                        };
                        if (!Enum.IsDefined(format)) return Fail(out arguments);
                        formatAssigned = true;
                        break;
                    case "--lifecycle-statuses" when lifecycleStatuses is null:
                        try { AdrLifecyclePolicy.Parse(value); }
                        catch (ArgumentException) { return Fail(out arguments); }
                        lifecycleStatuses = value;
                        break;
                    case "--filename-policy" when filenamePolicy is null:
                        try { AdrFilenamePolicy.Parse(value); }
                        catch (ArgumentException) { return Fail(out arguments); }
                        filenamePolicy = value;
                        break;
                    case "--validation-profile" when validationProfile is null:
                        try { AdrValidationOptionsFactory.ParseProfile(value); }
                        catch (ArgumentException) { return Fail(out arguments); }
                        validationProfile = value;
                        break;
                    default:
                        return Fail(out arguments);
                }

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

    private static bool Fail(out RelationshipsCommandArguments arguments)
    {
        arguments = new RelationshipsCommandArguments(".", AdrFormat.Canonical, null, false, null, null);
        return false;
    }
}
