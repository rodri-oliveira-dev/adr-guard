namespace AdrGuard.Configuration;

internal static class ConfigurationArguments
{
    internal static IReadOnlyList<string> Apply(
        IReadOnlyList<string> args,
        AdrGuardConfiguration? configuration)
    {
        ArgumentNullException.ThrowIfNull(args);

        // Preserve unmodified help handling even with repository configuration.
        if (configuration is null || args.Count == 0
            || (args.Count == 2 && args[1] is "-h" or "--help"))
        {
            return args;
        }

        var configured = args.ToList();
        switch (args[0])
        {
            case "check":
            case "index":
            case "baseline":
                AddDirectoryIfMissing(configured, configuration.AdrDirectoryPath);
                AddAdrFormatIfMissing(configured, configuration);
                AddLifecyclePolicyIfMissing(configured, configuration);
                AddConventionalSupersessionIfMissing(configured, configuration);
                AddFilenamePolicyIfMissing(configured, configuration);
                AddValidationProfileIfMissing(configured, configuration);
                break;
            case "new":
                AddDirectoryIfMissing(configured, configuration.AdrDirectoryPath);
                AddTemplateIfMissing(configured, configuration);
                AddFilenamePolicyIfMissing(configured, configuration);
                break;
            case "draft":
                AddDirectoryIfMissing(configured, configuration.AdrDirectoryPath);
                AddTemplateIfMissing(configured, configuration);
                break;
        }

        if (args[0] == "check" && configuration.PlaceholderPolicy is not null
            && !configured.Contains("--placeholder-policy", StringComparer.Ordinal))
        {
            configured.Add("--placeholder-policy");
            configured.Add(configuration.PlaceholderPolicy);
        }
        if (args[0] == "check" && configuration.ValidateMetadata
            && !configured.Contains("--validate-metadata", StringComparer.Ordinal))
            configured.Add("--validate-metadata");

        return configured;
    }

    private static void AddDirectoryIfMissing(List<string> args, string directory)
    {
        if (!HasPositionalDirectory(args))
        {
            args.Insert(1, directory);
        }
    }

    private static bool HasPositionalDirectory(List<string> args)
    {
        for (var index = 1; index < args.Count; index++)
        {
            var argument = args[index];
            if (!argument.StartsWith('-'))
            {
                return true;
            }

            if (OptionTakesValue(argument) && index + 1 < args.Count)
            {
                index++;
            }
        }

        return false;
    }

    private static bool OptionTakesValue(string option) => option is
        "--catalog"
        or "--output"
        or "--title"
        or "--template"
        or "--template-file"
        or "--culture"
        or "--context"
        or "--provider"
        or "--model"
        or "--endpoint"
        or "--context-file"
        or "--policy"
        or "--policy-file"
        or "--format"
        or "--adr-format"
        or "--lifecycle-statuses"
        or "--filename-policy"
        or "--placeholder-policy"
        or "--validation-profile"
        or "--base-ref"
        or "--baseline"
        or "--compare-ref";

    private static void AddAdrFormatIfMissing(
        List<string> args,
        AdrGuardConfiguration configuration)
    {
        if (configuration.AdrFormat is null
            || args.Contains("--adr-format", StringComparer.Ordinal))
        {
            return;
        }

        args.Add("--adr-format");
        args.Add(configuration.AdrFormat);
    }

    private static void AddTemplateIfMissing(
        List<string> args,
        AdrGuardConfiguration configuration)
    {
        if (args.Contains("--template", StringComparer.Ordinal)
            || args.Contains("--template-file", StringComparer.Ordinal))
        {
            return;
        }

        if (configuration.TemplateFile is not null)
        {
            args.Add("--template-file");
            args.Add(configuration.TemplateFilePath!);
        }
        else if (configuration.Template is not null)
        {
            args.Add("--template");
            args.Add(configuration.Template);
        }
    }

    private static void AddLifecyclePolicyIfMissing(
        List<string> args,
        AdrGuardConfiguration configuration)
    {
        if (configuration.LifecycleStatuses is null
            || args.Contains("--lifecycle-statuses", StringComparer.Ordinal))
        {
            return;
        }

        args.Add("--lifecycle-statuses");
        args.Add(configuration.LifecycleStatuses);
    }

    private static void AddConventionalSupersessionIfMissing(
        List<string> args,
        AdrGuardConfiguration configuration)
    {
        if (configuration.ConventionalSupersession
            && !args.Contains("--conventional-supersession", StringComparer.Ordinal))
        {
            args.Add("--conventional-supersession");
        }
    }

    private static void AddFilenamePolicyIfMissing(List<string> args, AdrGuardConfiguration configuration)
    {
        if (configuration.FilenamePolicy is null || args.Contains("--filename-policy", StringComparer.Ordinal)) return;
        args.Add("--filename-policy");
        args.Add(configuration.FilenamePolicy);
    }

    private static void AddValidationProfileIfMissing(List<string> args, AdrGuardConfiguration configuration)
    {
        if (configuration.ValidationProfile is null || args.Contains("--validation-profile", StringComparer.Ordinal)) return;
        args.Add("--validation-profile");
        args.Add(configuration.ValidationProfile);
    }
}
