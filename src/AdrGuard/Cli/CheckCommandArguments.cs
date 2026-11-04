using AdrGuard.Git;
using AdrGuard.Parsing;
using AdrGuard.Validation;

namespace AdrGuard.Cli;

internal sealed record CheckCommandArguments(
    string DirectoryPath,
    CheckOutputFormat Format,
    AdrFormat AdrFormat,
    string? LifecycleStatuses,
    bool ConventionalSupersession,
    string? FilenamePolicy,
    string? PlaceholderPolicy,
    bool ValidateMetadata,
    string? ValidationProfile,
    bool Changed,
    string? BaseReference,
    string? BaselinePath,
    bool DiagnosticLocations)
{
    internal static bool TryParse(
        IReadOnlyList<string> args,
        out CheckCommandArguments arguments)
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
        return state.Changed == (state.BaseReference is not null);
    }

    private static bool TryApplyArgument(
        IReadOnlyList<string> args,
        ref int index,
        ParseState state) =>
        args[index] switch
        {
            "--diagnostic-locations" => TryAssignFlag(ref state.DiagnosticLocations),
            "--validate-metadata" => TryAssignFlag(ref state.ValidateMetadata),
            "--conventional-supersession" => TryAssignFlag(ref state.ConventionalSupersession),
            "--changed" => TryAssignFlag(ref state.Changed),
            "--base-ref" => TryAssignBaseReference(args, ref index, state),
            "--baseline" => TryAssignBaseline(args, ref index, state),
            "--format" => TryAssignFormat(args, ref index, state),
            "--adr-format" => TryAssignAdrFormat(args, ref index, state),
            "--lifecycle-statuses" => TryAssignLifecycleStatuses(args, ref index, state),
            "--filename-policy" => TryAssignFilenamePolicy(args, ref index, state),
            "--placeholder-policy" => TryAssignPlaceholderPolicy(args, ref index, state),
            "--validation-profile" => TryAssignValidationProfile(args, ref index, state),
            _ => TryAssignDirectory(args[index], state),
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

    private static bool TryAssignBaseReference(
        IReadOnlyList<string> args,
        ref int index,
        ParseState state)
    {
        if (state.BaseReference is not null
            || !TryReadValue(args, ref index, rejectOption: false, out var value))
        {
            return false;
        }

        try
        {
            GitChangeDetector.ValidateReference(value);
        }
        catch (ArgumentException)
        {
            return false;
        }

        state.BaseReference = value;
        return true;
    }

    private static bool TryAssignBaseline(
        IReadOnlyList<string> args,
        ref int index,
        ParseState state)
    {
        if (state.BaselinePath is not null
            || !TryReadValue(args, ref index, rejectOption: true, out var value))
        {
            return false;
        }

        state.BaselinePath = value;
        return true;
    }

    private static bool TryAssignFormat(
        IReadOnlyList<string> args,
        ref int index,
        ParseState state)
    {
        if (state.FormatAssigned
            || !TryReadValue(args, ref index, rejectOption: false, out var value))
        {
            return false;
        }

        state.Format = value switch
        {
            "text" => CheckOutputFormat.Text,
            "json" => CheckOutputFormat.Json,
            "sarif" => CheckOutputFormat.Sarif,
            _ => (CheckOutputFormat)(-1),
        };
        state.FormatAssigned = Enum.IsDefined(state.Format);
        return state.FormatAssigned;
    }

    private static bool TryAssignAdrFormat(
        IReadOnlyList<string> args,
        ref int index,
        ParseState state)
    {
        if (state.AdrFormatAssigned
            || !TryReadValue(args, ref index, rejectOption: false, out var value))
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
            || !TryReadValue(args, ref index, rejectOption: false, out var value))
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
            || !TryReadValue(args, ref index, rejectOption: false, out var value))
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

    private static bool TryAssignPlaceholderPolicy(
        IReadOnlyList<string> args,
        ref int index,
        ParseState state)
    {
        if (state.PlaceholderPolicy is not null
            || !TryReadValue(args, ref index, rejectOption: false, out var value)
            || value is not ("off" or "warn" or "error"))
        {
            return false;
        }

        state.PlaceholderPolicy = value;
        return true;
    }

    private static bool TryAssignValidationProfile(
        IReadOnlyList<string> args,
        ref int index,
        ParseState state)
    {
        if (state.ValidationProfile is not null
            || !TryReadValue(args, ref index, rejectOption: false, out var value))
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

    private static bool TryAssignDirectory(string value, ParseState state)
    {
        if (value.StartsWith('-') || state.DirectoryAssigned)
        {
            return false;
        }

        state.DirectoryPath = value;
        state.DirectoryAssigned = true;
        return true;
    }

    private static bool TryReadValue(
        IReadOnlyList<string> args,
        ref int index,
        bool rejectOption,
        out string value)
    {
        if (index + 1 >= args.Count)
        {
            value = string.Empty;
            return false;
        }

        value = args[++index];
        return !string.IsNullOrWhiteSpace(value)
            && (!rejectOption || !value.StartsWith('-'));
    }

    private static CheckCommandArguments Empty { get; } =
        new(
            ".",
            CheckOutputFormat.Text,
            AdrFormat.Canonical,
            null,
            false,
            null,
            null,
            false,
            null,
            false,
            null,
            null,
            false);

    private sealed class ParseState
    {
        internal string DirectoryPath { get; set; } = ".";
        internal CheckOutputFormat Format { get; set; } = CheckOutputFormat.Text;
        internal AdrFormat AdrFormat { get; set; } = AdrFormat.Canonical;
        internal string? LifecycleStatuses { get; set; }
        internal bool ConventionalSupersession;
        internal string? FilenamePolicy { get; set; }
        internal string? PlaceholderPolicy { get; set; }
        internal bool ValidateMetadata;
        internal string? ValidationProfile { get; set; }
        internal bool Changed;
        internal string? BaseReference { get; set; }
        internal string? BaselinePath { get; set; }
        internal bool DiagnosticLocations;
        internal bool DirectoryAssigned { get; set; }
        internal bool FormatAssigned { get; set; }
        internal bool AdrFormatAssigned { get; set; }

        internal CheckCommandArguments ToArguments() =>
            new(
                DirectoryPath,
                Format,
                AdrFormat,
                LifecycleStatuses,
                ConventionalSupersession,
                FilenamePolicy,
                PlaceholderPolicy,
                ValidateMetadata,
                ValidationProfile,
                Changed,
                BaseReference,
                BaselinePath,
                DiagnosticLocations);
    }
}
