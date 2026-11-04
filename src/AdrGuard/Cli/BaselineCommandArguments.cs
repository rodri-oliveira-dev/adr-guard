using AdrGuard.Parsing;
using AdrGuard.Validation;

namespace AdrGuard.Cli;

internal sealed record BaselineCommandArguments(
    string DirectoryPath,
    string OutputPath,
    bool Update,
    AdrFormat Format,
    string? LifecycleStatuses,
    bool ConventionalSupersession,
    string? FilenamePolicy,
    string? ValidationProfile)
{
    internal static bool TryParse(
        IReadOnlyList<string> args,
        out BaselineCommandArguments arguments)
    {
        var state = new ParseState();

        for (var index = 1; index < args.Count; index++)
        {
            if (!TryApplyArgument(args, ref index, state))
            {
                arguments = Empty;
                return false;
            }
        }

        arguments = state.ToArguments();
        return state.OutputPath is not null;
    }

    private static bool TryApplyArgument(
        IReadOnlyList<string> args,
        ref int index,
        ParseState state) =>
        args[index] switch
        {
            "--conventional-supersession" => TryAssignFlag(ref state.ConventionalSupersession),
            "--update" => TryAssignFlag(ref state.Update),
            "--output" => TryAssignOutput(args, ref index, state),
            "--adr-format" => TryAssignFormat(args, ref index, state),
            "--lifecycle-statuses" => TryAssignLifecycleStatuses(args, ref index, state),
            "--filename-policy" => TryAssignFilenamePolicy(args, ref index, state),
            "--validation-profile" => TryAssignValidationProfile(args, ref index, state),
            _ => CliCommandArgumentReader.TryAssignDirectory(
                args[index],
                ref state.DirectoryPath,
                ref state.DirectoryAssigned),
        };

    private static bool TryAssignFlag(ref bool target)
    {
        if (target)
        {
            return false;
        }

        target = true;
        return true;
    }

    private static bool TryAssignOutput(
        IReadOnlyList<string> args,
        ref int index,
        ParseState state)
    {
        if (state.OutputPath is not null
            || !TryReadValue(args, ref index, out var value)
            || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        state.OutputPath = value;
        return true;
    }

    private static bool TryAssignFormat(
        IReadOnlyList<string> args,
        ref int index,
        ParseState state)
    {
        if (state.FormatAssigned
            || !TryReadValue(args, ref index, out var value))
        {
            return false;
        }

        state.Format = value switch
        {
            "canonical" => AdrFormat.Canonical,
            "madr-4" => AdrFormat.Madr4,
            _ => (AdrFormat)(-1),
        };
        state.FormatAssigned = Enum.IsDefined(state.Format);
        return state.FormatAssigned;
    }

    private static bool TryAssignLifecycleStatuses(
        IReadOnlyList<string> args,
        ref int index,
        ParseState state)
    {
        if (!TryReadValue(args, ref index, out var value))
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
        if (!TryReadValue(args, ref index, out var value))
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
        if (!TryReadValue(args, ref index, out var value))
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

    private static bool TryReadValue(
        IReadOnlyList<string> args,
        ref int index,
        out string value) =>
        CliCommandArgumentReader.TryReadValue(
            args,
            ref index,
            rejectWhitespace: false,
            out value);

    private static BaselineCommandArguments Empty { get; } =
        new(
            ".",
            string.Empty,
            false,
            AdrFormat.Canonical,
            null,
            false,
            null,
            null);

    private sealed class ParseState
    {
        internal string DirectoryPath = ".";
        internal string? OutputPath { get; set; }
        internal bool Update;
        internal AdrFormat Format { get; set; } = AdrFormat.Canonical;
        internal string? LifecycleStatuses { get; set; }
        internal bool ConventionalSupersession;
        internal string? FilenamePolicy { get; set; }
        internal string? ValidationProfile { get; set; }
        internal bool DirectoryAssigned;
        internal bool FormatAssigned { get; set; }

        internal BaselineCommandArguments ToArguments() =>
            new(
                DirectoryPath,
                OutputPath ?? string.Empty,
                Update,
                Format,
                LifecycleStatuses,
                ConventionalSupersession,
                FilenamePolicy,
                ValidationProfile);
    }
}
