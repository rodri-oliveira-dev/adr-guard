using System.Text;
using AdrGuard.Configuration;
using AdrGuard.Generation;

namespace AdrGuard.Cli;

internal static class InitCommand
{
    private const string ConfigurationFileName = ".adrguard.yml";
    private const string WorkflowRelativePath = ".github/workflows/adr-guard.yml";

    internal const string HelpText = """
        Usage:
          adr-guard init [repository] [--adr-directory <path>] [--template minimal|extended | --template-file <path>] [--github-actions] [--dry-run] [--overwrite]

        Initialize ADR Guard in an existing repository. The repository defaults to the current directory.
        Paths must be repository-relative and cannot traverse symbolic links or escape the repository.
        Existing files are never replaced unless --overwrite is supplied. Repeating the same command is idempotent.
        --dry-run reports planned changes without writing files or directories.
        """;

    internal static int Run(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Count == 2 && args[1] is "-h" or "--help")
        {
            output.WriteLine(HelpText);
            return ExitCodes.Success;
        }

        if (!TryParse(args, out var options))
        {
            error.WriteLine("Invalid arguments for 'init'.");
            error.WriteLine("Run 'adr-guard init --help' for usage.");
            return ExitCodes.UsageError;
        }

        try
        {
            return Execute(options, output, error);
        }
        catch (ArgumentException exception)
        {
            error.WriteLine($"Invalid init configuration: {exception.Message}");
            return ExitCodes.UsageError;
        }
        catch (IOException exception)
        {
            error.WriteLine($"Unable to initialize ADR Guard: {exception.Message}");
            return ExitCodes.OperationalError;
        }
        catch (UnauthorizedAccessException exception)
        {
            error.WriteLine($"Unable to initialize ADR Guard: {exception.Message}");
            return ExitCodes.OperationalError;
        }
    }

    private static int Execute(
        InitOptions options,
        TextWriter output,
        TextWriter error)
    {
        var repositoryRoot = Path.GetFullPath(options.RepositoryPath);
        if (!Directory.Exists(repositoryRoot))
        {
            error.WriteLine($"Repository directory does not exist: '{options.RepositoryPath}'.");
            return ExitCodes.OperationalError;
        }

        var adrDirectory = RepositoryPath.NormalizeRelative(repositoryRoot, options.AdrDirectory);
        string? templateFile = null;

        if (options.TemplateFile is not null)
        {
            templateFile = RepositoryPath.NormalizeRelative(repositoryRoot, options.TemplateFile);
            var templatePath = RepositoryPath.ResolveContained(
                repositoryRoot,
                templateFile,
                allowMissingLeaf: false);

            if (!File.Exists(templatePath)
                || !string.Equals(Path.GetExtension(templatePath), ".md", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("--template-file must identify an existing Markdown file inside the repository.");
            }
        }

        if (options.Template is not null
            && !AdrBuiltInTemplates.Names.Contains(options.Template, StringComparer.Ordinal))
        {
            throw new ArgumentException("--template must be 'minimal' or 'extended'.");
        }

        var configPath = RepositoryPath.ResolveContained(repositoryRoot, ConfigurationFileName);
        var config = RenderConfiguration(adrDirectory, options.Template, templateFile);
        var changes = new List<PlannedFile>
        {
            new(configPath, config),
        };

        if (options.CreateGitHubActions)
        {
            changes.Add(new PlannedFile(
                RepositoryPath.ResolveContained(repositoryRoot, WorkflowRelativePath),
                RenderWorkflow(adrDirectory)));
        }

        var conflict = changes.FirstOrDefault(change =>
            File.Exists(change.Path)
            && !string.Equals(File.ReadAllText(change.Path), change.Content, StringComparison.Ordinal)
            && !options.Overwrite);

        if (conflict is not null)
        {
            error.WriteLine($"Refusing to overwrite existing file '{conflict.Path}'. Use --overwrite to authorize replacement.");
            return ExitCodes.OperationalError;
        }

        ReportOrCreateDirectory(
            RepositoryPath.ResolveContained(repositoryRoot, adrDirectory),
            options.DryRun,
            output);

        foreach (var change in changes)
        {
            ApplyFile(change, options.DryRun, output);
        }

        output.WriteLine(options.DryRun
            ? "Dry run completed; no files were changed."
            : "ADR Guard initialization completed.");
        return ExitCodes.Success;
    }

    private static void ReportOrCreateDirectory(
        string path,
        bool dryRun,
        TextWriter output)
    {
        if (Directory.Exists(path))
        {
            output.WriteLine($"Unchanged directory: {path}");
            return;
        }

        output.WriteLine($"{(dryRun ? "Would create" : "Created")} directory: {path}");
        if (!dryRun)
        {
            Directory.CreateDirectory(path);
        }
    }

    private static void ApplyFile(
        PlannedFile file,
        bool dryRun,
        TextWriter output)
    {
        if (File.Exists(file.Path)
            && string.Equals(File.ReadAllText(file.Path), file.Content, StringComparison.Ordinal))
        {
            output.WriteLine($"Unchanged file: {file.Path}");
            return;
        }

        output.WriteLine($"{(dryRun ? "Would write" : "Wrote")} file: {file.Path}");
        if (dryRun)
        {
            return;
        }

        var parent = Path.GetDirectoryName(file.Path)!;
        Directory.CreateDirectory(parent);
        File.WriteAllText(file.Path, file.Content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string RenderConfiguration(
        string adrDirectory,
        string? template,
        string? templateFile)
    {
        var builder = new StringBuilder()
            .AppendLine("schema-version: 1")
            .Append("adr-directory: ").AppendLine(Quote(adrDirectory));

        if (templateFile is not null)
        {
            builder.Append("template-file: ").AppendLine(Quote(templateFile));
        }
        else
        {
            builder.Append("template: ").AppendLine(template ?? AdrBuiltInTemplates.Minimal);
        }

        return builder.ToString().ReplaceLineEndings("\n");
    }

    private static string RenderWorkflow(string adrDirectory) =>
        $$"""
        name: ADR validation

        on:
          pull_request:
          push:
            branches: [main]

        permissions:
          contents: read

        jobs:
          adr-guard:
            runs-on: ubuntu-latest
            steps:
              - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
                with:
                  persist-credentials: false
              - uses: rodri-oliveira-dev/adr-guard@v1
                with:
                  command: check
                  path: {{adrDirectory}}
        """.ReplaceLineEndings("\n") + "\n";

    private static string Quote(string value) =>
        '"' + value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal) + '"';

    private static bool TryParse(
        IReadOnlyList<string> args,
        out InitOptions options)
    {
        var repository = ".";
        var adrDirectory = "docs/adr";
        string? template = null;
        string? templateFile = null;
        var githubActions = false;
        var dryRun = false;
        var overwrite = false;
        var repositoryAssigned = false;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 1; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument is "--github-actions" or "--dry-run" or "--overwrite")
            {
                if (!seen.Add(argument))
                {
                    options = default!;
                    return false;
                }

                githubActions |= argument == "--github-actions";
                dryRun |= argument == "--dry-run";
                overwrite |= argument == "--overwrite";
                continue;
            }

            if (argument is "--adr-directory" or "--template" or "--template-file")
            {
                if (!seen.Add(argument)
                    || index + 1 >= args.Count
                    || string.IsNullOrWhiteSpace(args[index + 1])
                    || args[index + 1].StartsWith('-'))
                {
                    options = default!;
                    return false;
                }

                var value = args[++index];
                switch (argument)
                {
                    case "--adr-directory":
                        adrDirectory = value;
                        break;
                    case "--template":
                        template = value;
                        break;
                    case "--template-file":
                        templateFile = value;
                        break;
                }

                continue;
            }

            if (argument.StartsWith('-') || repositoryAssigned)
            {
                options = default!;
                return false;
            }

            repository = argument;
            repositoryAssigned = true;
        }

        if (template is not null && templateFile is not null)
        {
            options = default!;
            return false;
        }

        options = new InitOptions(
            repository,
            adrDirectory,
            template,
            templateFile,
            githubActions,
            dryRun,
            overwrite);
        return true;
    }

    private sealed record InitOptions(
        string RepositoryPath,
        string AdrDirectory,
        string? Template,
        string? TemplateFile,
        bool CreateGitHubActions,
        bool DryRun,
        bool Overwrite);

    private sealed record PlannedFile(string Path, string Content);
}
