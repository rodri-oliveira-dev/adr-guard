using AdrGuard.Validation;

namespace AdrGuard.Cli;

internal sealed record ExportRelationshipsCommandArguments(
    string DirectoryPath,
    string? OutputPath,
    AdrFormat AdrFormat,
    string? LifecycleStatuses,
    bool ConventionalSupersession,
    string? FilenamePolicy,
    string? ValidationProfile)
{
    internal static bool TryParse(
        IReadOnlyList<string> args,
        out ExportRelationshipsCommandArguments arguments)
    {
        if (args.Count < 2 || args[0] != "export" || args[1] != "relationships")
        {
            arguments = Empty;
            return false;
        }

        var state = new ParseState();
        for (var index = 2; index < args.Count; index++)
        {
            if (!TryApplyArgument(args, ref index, state))
            {
                arguments = Empty;
                return false;
            }
        }

        arguments = state.ToArguments();
        return true;
    }

    private static bool TryApplyArgument(
        IReadOnlyList<string> args,
        ref int index,
        ParseState state) =>
        args[index] switch
        {
            "--output" => TryAssignValue(args, ref index, ref state.OutputPath),
            "--adr-format" => TryAssignFormat(args, ref index, state),
            "--lifecycle-statuses" => TryAssignLifecycleStatuses(args, ref index, state),
            "--conventional-supersession" => TryAssignFlag(ref state.ConventionalSupersession),
            "--filename-policy" => TryAssignFilenamePolicy(args, ref index, state),
            "--validation-profile" => TryAssignValidationProfile(args, ref index, state),
            _ => CliCommandArgumentReader.TryAssignDirectory(
                args[index],
                ref state.DirectoryPath,
                ref state.DirectoryAssigned),
        };

    private static bool TryAssignFlag(ref bool target)
    {
        if (target) return false;
        target = true;
        return true;
    }

    private static bool TryAssignValue(
        IReadOnlyList<string> args,
        ref int index,
        ref string? target)
    {
        if (target is not null
            || !CliCommandArgumentReader.TryReadValue(args, ref index, true, out var value))
        {
            return false;
        }

        target = value;
        return true;
    }

    private static bool TryAssignFormat(
        IReadOnlyList<string> args,
        ref int index,
        ParseState state)
    {
        if (state.AdrFormatAssigned
            || !CliCommandArgumentReader.TryReadValue(args, ref index, true, out var value))
        {
            return false;
        }

        state.AdrFormat = value switch
        {
            "canonical" => AdrFormat.Canonical,
            "madr-4" => AdrFormat.Madr4,
            _ => (AdrFormat)(-1),
        };
        state.AdrFormatAssigned = Enum.IsDefined(state.AdrFormat);
        return state.AdrFormatAssigned;
    }

    private static bool TryAssignLifecycleStatuses(
        IReadOnlyList<string> args,
        ref int index,
        ParseState state)
    {
        if (state.LifecycleStatuses is not null
            || !CliCommandArgumentReader.TryReadValue(args, ref index, true, out var value))
        {
            return false;
        }

        try
        {
            AdrLifecyclePolicy.Parse(value);
        }
        catch (ArgumentException)
        {
            return false;
        }

        state.LifecycleStatuses = value;
        return true;
    }

    private static bool TryAssignFilenamePolicy(
        IReadOnlyList<string> args,
        ref int index,
        ParseState state)
    {
        if (state.FilenamePolicy is not null
            || !CliCommandArgumentReader.TryReadValue(args, ref index, true, out var value))
        {
            return false;
        }

        try
        {
            AdrFilenamePolicy.Parse(value);
        }
        catch (ArgumentException)
        {
            return false;
        }

        state.FilenamePolicy = value;
        return true;
    }

    private static bool TryAssignValidationProfile(
        IReadOnlyList<string> args,
        ref int index,
        ParseState state)
    {
        if (state.ValidationProfile is not null
            || !CliCommandArgumentReader.TryReadValue(args, ref index, true, out var value))
        {
            return false;
        }

        try
        {
            AdrValidationOptionsFactory.ParseProfile(value);
        }
        catch (ArgumentException)
        {
            return false;
        }

        state.ValidationProfile = value;
        return true;
    }

    private static ExportRelationshipsCommandArguments Empty { get; } =
        new(".", null, AdrFormat.Canonical, null, false, null, null);

    private sealed class ParseState
    {
        internal string DirectoryPath = ".";
        internal string? OutputPath;
        internal AdrFormat AdrFormat = AdrFormat.Canonical;
        internal string? LifecycleStatuses;
        internal bool ConventionalSupersession;
        internal string? FilenamePolicy;
        internal string? ValidationProfile;
        internal bool DirectoryAssigned;
        internal bool AdrFormatAssigned;

        internal ExportRelationshipsCommandArguments ToArguments() =>
            new(
                DirectoryPath,
                OutputPath,
                AdrFormat,
                LifecycleStatuses,
                ConventionalSupersession,
                FilenamePolicy,
                ValidationProfile);
    }
}
