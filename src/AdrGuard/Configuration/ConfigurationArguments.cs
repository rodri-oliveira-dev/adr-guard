namespace AdrGuard.Configuration;

internal static class ConfigurationArguments
{
    internal static IReadOnlyList<string> Apply(
        IReadOnlyList<string> args,
        AdrGuardConfiguration? configuration)
    {
        ArgumentNullException.ThrowIfNull(args);

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
                AddDirectoryIfMissing(configured, configuration.AdrDirectoryPath);
                break;
            case "new":
            case "draft":
                AddDirectoryIfMissing(configured, configuration.AdrDirectoryPath);
                AddTemplateIfMissing(configured, configuration);
                break;
        }

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
        "--output"
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
        or "--format";

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
}
