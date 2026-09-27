using AdrGuard.Generation;
using AdrGuard.Generation.Providers;
using AdrGuard.Review;
using AdrGuard.Review.Policy;
using AdrGuard.Review.Providers;
using AdrGuard.Review.Reporting;
using AdrGuard.Review.Security;
using System.Reflection;

namespace AdrGuard.Cli;

internal static class CliApplication
{
    private const string DefaultDraftCultureName = "en-US";

    private const string HelpText = """
        ADR Guard

        Validate and maintain Architecture Decision Records from the command line.

        Usage:
          adr-guard check [directory]
          adr-guard index [directory] [--output <file>]
          adr-guard new [adr-directory] --title <title> [--template minimal|extended] [--template-file <path>] [--culture en-US|pt-BR] [--dry-run|--preview]
          adr-guard draft [directory] --title <title> --context <context> --provider <provider> --model <model> [--culture <name>] [--template minimal|extended | --template-file <path>] [--endpoint <uri>] [--context-file <path>]... [--include-existing-adrs] [--dry-run|--preview]
          adr-guard review <adr-file> --provider <provider> --model <model> [--endpoint <uri>] [--context-file <path>]... [--include-existing-adrs] [--policy advisory|enforce] [--policy-file <path>] [--format text|json] [--output <path> [--overwrite]]
          adr-guard [options]

        Commands:
          check    Validate ADR files. Defaults to the current directory.
          index    Validate ADR files and generate an index. Defaults to README.md.
          new      Create a Proposed ADR from an offline Markdown template.
          draft    Generate a Proposed ADR draft through a configured AI provider.
          review   Request an advisory, read-only technical review of one existing ADR.

        Options:
          -h, --help    Show command-line help.
          --version     Show the application version.

        Exit codes:
          0  Success
          1  ADR validation failed
          2  Invalid command-line usage
          3  Operational error
          4  Deterministic review policy failed
        """;

    private const string CheckHelpText = """
        Usage:
          adr-guard check [directory]

        Validate ADR files recursively. The directory defaults to the current directory.
        """;

    private const string IndexHelpText = """
        Usage:
          adr-guard index [directory] [--output <file>]

        Validate ADR files and generate a Markdown index.
        The directory defaults to the current directory.
        The output defaults to README.md inside the ADR directory.
        Relative --output paths are resolved from the current working directory.
        """;

    private const string DraftHelpText = """
        Usage:
          adr-guard draft [directory] --title <title> --context <context> --provider <provider> --model <model> [--culture <name>] [--template minimal|extended | --template-file <path>] [--endpoint <uri>] [--context-file <path>]... [--include-existing-adrs] [--dry-run|--preview]

        Generate a Proposed ADR draft through a configured AI provider.
        The directory defaults to the current directory.

        Required provider options:
          --provider <provider>   openai | anthropic | gemini | openai-compatible
          --model <model>         Provider model identifier. ADR Guard does not choose a default model.

        Optional options:
          --culture <name>        .NET globalization culture name such as en-US or pt-BR.
                                  Defaults to en-US. Template selection supports only en-US/pt-BR.
          --template <name>       Optional offline Markdown template: minimal | extended.
          --template-file <path>  Optional explicit local .md file, exclusive with --template.
                                  Resolved from the invocation working directory.
                                  Template bodies and guidance are not sent to the AI provider.
          --endpoint <uri>        Required only for openai-compatible; rejected for official providers.
          --context-file <path>    Add an explicit .md or .txt context file. May be repeated.
                                  Relative paths are resolved from the current working directory.
          --include-existing-adrs   Opt in to sending parsed existing ADR context to the provider.
                                  ADRs are ordered deterministically and bounded to 12000 characters.
          --dry-run, --preview      Generate and validate the ADR without writing a file.

        Context limits:
          --context               20000 characters maximum.
          each --context-file     50000 characters maximum.
          all --context-file      100000 characters maximum in aggregate.
          composed context        120000 characters maximum.
          existing ADR context    12000 characters maximum.

        Authentication is read only from environment variables:
          openai                  OPENAI_API_KEY
          anthropic               ANTHROPIC_API_KEY
          gemini                  GEMINI_API_KEY
          openai-compatible       ADR_GUARD_OPENAI_COMPATIBLE_API_KEY (optional)

        Oversized context is rejected before provider invocation and is never silently truncated.
        ADR structural headings and the Proposed status remain canonical.
        Generated content is validated before a new ADR file is written.
        Ctrl+C cancels the draft workflow through context loading, provider calls, validation, and persistence.
        Persisted drafts are written to a temporary file and atomically promoted without overwrite.
        Dry-run/preview uses the same deterministic ID and filename calculation,
        validates the generated ADR, prints it, and does not write any file.
        """;

    private const string ReviewHelpText = """
        Usage:
          adr-guard review <adr-file> --provider <provider> --model <model> [--endpoint <uri>] [--context-file <path>]... [--include-existing-adrs] [--policy advisory|enforce] [--policy-file <path>] [--format text|json] [--output <path> [--overwrite]]

        Request an AI-assisted technical review of one existing, structurally valid ADR.
        The command is advisory and read-only: it does not edit the ADR, change its status,
        update an index, accept/reject the decision, or alter git state.

        Required provider options:
          --provider <provider>   openai | anthropic | gemini | openai-compatible
          --model <model>         Provider model identifier. ADR Guard does not choose a default model.

        Optional options:
          --endpoint <uri>        Required only for openai-compatible; rejected for official providers.
          --context-file <path>    Explicit UTF-8 .md or .txt context file; repeatable.
                                  Each file is limited to 50000 characters and 150000 bytes.
                                  Aggregate limits are 100000 characters and 300000 bytes.
          --include-existing-adrs  Opt in to bounded parsed ADR context from the target ADR directory.
                                  The selected target ADR is deduplicated from this set.
          --policy <mode>           Deterministic policy mode: advisory | enforce. Defaults to advisory.
                                  AI findings never become enforceable policy violations.
          --policy-file <path>      Strict local JSON policy (schemaVersion 1.0). Not sent to the provider.
                                  Required with --policy enforce.
          --format <format>         Report format: text | json. Defaults to text.
                                  JSON writes one versioned report object to stdout.
          --output <path>          Explicitly persist the rendered report. Text output requires .md/.txt;
                                  JSON output requires .json. Parent directory must already exist.
          --overwrite              Allow --output to replace an existing regular file atomically.
                                  Rejected without --output; never permits replacing the ADR or README.md index.

        Policy:
          Advisory is the safe default. Deterministic policy violations are diagnostics only.
          Enforce mode exits 4 only for named local rules with inspectable evidence, before provider invocation.
          Supported v1 rules: required-section-content and required-context-file.
          Model wording such as critical/high and model-reported missing context remain advisory.
          See docs/adr-review-policy-v1.md for the policy schema and outcome/exit-code matrix.

        Reporting:
          Text output is Markdown-compatible and includes evidence, unknowns, follow-up priority and
          the human-review caveat. JSON uses schemaVersion 1.0 with stable camelCase field names.
          In --format json mode, pre-provider source disclosure is written to stderr so stdout stays valid JSON.
          Report files are UTF-8 without BOM and are written only when --output is explicitly supplied.
          Provider token usage/charges may apply; ADR Guard does not fabricate or estimate precise costs.

        Privacy and limits:
          Review sends only the selected ADR by default.
          Context files are never discovered automatically and must be explicitly supplied.
          Existing ADRs are included only with --include-existing-adrs and are bounded to 12000 characters.
          The final composed review context is limited to 120000 characters.
          Every transmitted source is disclosed locally before provider invocation.
          Source IDs plus filenames are used in provider context; absolute local paths are not included.
          UTF-16, UTF-32, invalid UTF-8 and binary/NUL explicit context are rejected.
          No repository/source-tree discovery occurs by default. --include-existing-adrs explicitly authorizes
          Markdown ADR discovery below the target ADR directory; git diffs and environment variables are never scanned as context.

        Security boundary:
          ADR/context text and provider output are untrusted data. Embedded instructions, URLs and commands are inert.
          Review exposes no filesystem/network/tool execution, file-write, status-change or secret-access capability to the model.
          Known provider/GitHub credential values are redacted from provider context, diagnostics and reports.
          Provider findings are field-bounded and workflow-command delimiters are neutralized before rendering.
          See docs/adr-review-security.md for fork/community PR and secret-handling guidance.

        Provider-side processing:
          Selected review material is transmitted to the configured external AI provider and can leave
          the local machine/process. Provider retention, logging, residency and processing terms apply;
          review the selected provider's privacy/data-processing policy before sending sensitive material.

        Authentication is read from the same provider environment variables used by 'draft'.
        Ctrl+C cancels provider execution.

        Exit codes:
          0  Review completed
          1  Selected ADR failed structural validation
          2  Invalid review command/provider/policy usage
          3  Operational/provider/cancellation/malformed-result failure
          4  Named deterministic enforcement rule violated
        """;

    internal static int Run(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        IAdrGenerationProvider? generationProvider = null,
        Func<HttpClient>? httpClientFactory = null,
        Func<string, string?>? environmentVariableReader = null,
        IAdrReviewProvider? reviewProvider = null) =>
        RunCore(
            args,
            output,
            error,
            generationProvider,
            httpClientFactory,
            environmentVariableReader,
            reviewProvider,
            default);

    internal static int Run(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken,
        IAdrGenerationProvider? generationProvider = null,
        Func<HttpClient>? httpClientFactory = null,
        Func<string, string?>? environmentVariableReader = null,
        IAdrReviewProvider? reviewProvider = null) =>
        RunCore(
            args,
            output,
            error,
            generationProvider,
            httpClientFactory,
            environmentVariableReader,
            reviewProvider,
            cancellationToken);

    private static int RunCore(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        IAdrGenerationProvider? generationProvider,
        Func<HttpClient>? httpClientFactory,
        Func<string, string?>? environmentVariableReader,
        IAdrReviewProvider? reviewProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Count == 0 || IsHelpRequest(args))
        {
            output.WriteLine(HelpText);
            return ExitCodes.Success;
        }

        if (IsVersionRequest(args))
        {
            output.WriteLine(GetVersion());
            return ExitCodes.Success;
        }

        return args[0] switch
        {
            "check" => RunCheck(args, output, error),
            "index" => RunIndex(args, output, error),
            "new" => NewCommand.Run(args, output, error, cancellationToken),
            "draft" => RunDraft(
                args,
                output,
                error,
                generationProvider,
                httpClientFactory,
                environmentVariableReader,
                cancellationToken),
            "review" => RunReview(
                args,
                output,
                error,
                reviewProvider,
                httpClientFactory,
                environmentVariableReader,
                cancellationToken),
            _ => WriteUsageError(args, error),
        };
    }

    private static int RunCheck(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error)
    {
        if (args.Count == 2 && IsHelpOption(args[1]))
        {
            output.WriteLine(CheckHelpText);
            return ExitCodes.Success;
        }

        if (args.Count > 2)
        {
            return WriteCommandUsageError("check", error);
        }

        var directoryPath = args.Count == 2 ? args[1] : ".";

        if (directoryPath.StartsWith('-'))
        {
            return WriteCommandUsageError("check", error);
        }

        return CheckCommand.Run(directoryPath, output, error);
    }

    private static int RunIndex(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error)
    {
        if (args.Count == 2 && IsHelpOption(args[1]))
        {
            output.WriteLine(IndexHelpText);
            return ExitCodes.Success;
        }

        if (!TryParseIndexArguments(
                args,
                out var directoryPath,
                out var outputPath))
        {
            return WriteCommandUsageError("index", error);
        }

        return IndexCommand.Run(
            directoryPath,
            outputPath,
            output,
            error);
    }

    private static int RunReview(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        IAdrReviewProvider? injectedProvider,
        Func<HttpClient>? httpClientFactory,
        Func<string, string?>? environmentVariableReader,
        CancellationToken cancellationToken)
    {
        if (args.Count == 2 && IsHelpOption(args[1]))
        {
            output.WriteLine(ReviewHelpText);
            return ExitCodes.Success;
        }

        if (!TryParseReviewArguments(args, out var reviewArguments))
        {
            return WriteCommandUsageError("review", error);
        }

        if (string.IsNullOrWhiteSpace(reviewArguments.ProviderName)
            || string.IsNullOrWhiteSpace(reviewArguments.Model))
        {
            error.WriteLine("'review' requires both --provider and --model.");
            error.WriteLine("Run 'adr-guard review --help' for usage.");
            return ExitCodes.UsageError;
        }

        if (reviewArguments.PolicyMode == AdrReviewPolicyMode.Enforce
            && string.IsNullOrWhiteSpace(reviewArguments.PolicyFilePath))
        {
            error.WriteLine(
                "'review --policy enforce' requires --policy-file with explicit deterministic rules.");
            error.WriteLine("Run 'adr-guard review --help' for usage.");
            return ExitCodes.UsageError;
        }

        var securityBoundary = new AdrReviewSecurityBoundary(
            AdrReviewSecurityBoundary.ReadCredentialValues(
                environmentVariableReader));

        try
        {
            if (injectedProvider is not null)
            {
                return ReviewCommand.Run(
                    reviewArguments.TargetPath,
                    reviewArguments.ContextFilePaths,
                    reviewArguments.IncludeExistingAdrs,
                    reviewArguments.ProviderName!,
                    reviewArguments.Model!,
                    reviewArguments.Format,
                    reviewArguments.OutputPath,
                    reviewArguments.OverwriteOutput,
                    reviewArguments.PolicyMode,
                    reviewArguments.PolicyFilePath,
                    securityBoundary,
                    () => injectedProvider!,
                    output,
                    error,
                    cancellationToken);
            }

            AdrReviewProviderFactory.ValidateSelection(
                reviewArguments.ProviderName!,
                reviewArguments.Endpoint);

            HttpClient? httpClient = null;

            try
            {
                return ReviewCommand.Run(
                    reviewArguments.TargetPath,
                    reviewArguments.ContextFilePaths,
                    reviewArguments.IncludeExistingAdrs,
                    reviewArguments.ProviderName!,
                    reviewArguments.Model!,
                    reviewArguments.Format,
                    reviewArguments.OutputPath,
                    reviewArguments.OverwriteOutput,
                    reviewArguments.PolicyMode,
                    reviewArguments.PolicyFilePath,
                    securityBoundary,
                    () =>
                    {
                        httpClient ??=
                            httpClientFactory?.Invoke()
                            ?? new HttpClient();

                        return AdrReviewProviderFactory.Create(
                            reviewArguments.ProviderName,
                            reviewArguments.Model,
                            reviewArguments.Endpoint,
                            httpClient,
                            environmentVariableReader);
                    },
                    output,
                    error,
                    cancellationToken);
            }
            finally
            {
                httpClient?.Dispose();
            }
        }
        catch (ArgumentException exception)
        {
            error.WriteLine(
                securityBoundary.SanitizeDiagnostic(
                    exception.Message));
            error.WriteLine("Run 'adr-guard review --help' for usage.");
            return ExitCodes.UsageError;
        }
        catch (InvalidOperationException exception)
        {
            error.WriteLine(
                securityBoundary.SanitizeDiagnostic(
                    exception.Message));
            return ExitCodes.OperationalError;
        }
    }

    private static int RunDraft(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        IAdrGenerationProvider? injectedProvider,
        Func<HttpClient>? httpClientFactory,
        Func<string, string?>? environmentVariableReader,
        CancellationToken cancellationToken)
    {
        if (args.Count == 2 && IsHelpOption(args[1]))
        {
            output.WriteLine(DraftHelpText);
            return ExitCodes.Success;
        }

        if (!TryParseDraftArguments(
                args,
                out var draftArguments))
        {
            return WriteCommandUsageError(
                "draft",
                error);
        }

        // Selected templates are validated before constructing or invoking a
        // provider. Unselected draft keeps its existing culture and output.
        AdrTemplateDefinition? selectedTemplate = null;
        if (draftArguments.TemplateName is not null
            || draftArguments.TemplateFilePath is not null)
        {
            if (draftArguments.TemplateName is not null
                && draftArguments.TemplateFilePath is not null)
            {
                error.WriteLine("--template and --template-file are mutually exclusive.");
                error.WriteLine("Run 'adr-guard draft --help' for usage.");
                return ExitCodes.UsageError;
            }

            if (!string.Equals(draftArguments.CultureName, "en-US", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(draftArguments.CultureName, "pt-BR", StringComparison.OrdinalIgnoreCase))
            {
                error.WriteLine("Selected templates support --culture en-US or pt-BR only; unselected draft retains all supported .NET cultures.");
                error.WriteLine("Run 'adr-guard draft --help' for usage.");
                return ExitCodes.UsageError;
            }

            if (draftArguments.TemplateName is not null
                && !AdrBuiltInTemplates.Names.Contains(draftArguments.TemplateName, StringComparer.Ordinal))
            {
                error.WriteLine($"Unknown template '{draftArguments.TemplateName}'. Use minimal or extended.");
                error.WriteLine("Run 'adr-guard draft --help' for usage.");
                return ExitCodes.UsageError;
            }

            try
            {
                var templateCultureName = string.Equals(
                    draftArguments.CultureName,
                    "pt-BR",
                    StringComparison.OrdinalIgnoreCase)
                    ? "pt-BR"
                    : "en-US";

                selectedTemplate = AdrTemplateSelection.Resolve(
                    draftArguments.TemplateName,
                    draftArguments.TemplateFilePath,
                    templateCultureName,
                    cancellationToken: cancellationToken);
            }
            catch (ArgumentException exception)
            {
                error.WriteLine(exception.Message);
                error.WriteLine("Run 'adr-guard draft --help' for usage.");
                return ExitCodes.UsageError;
            }
            catch (Exception exception) when (exception is InvalidDataException
                or IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or OperationCanceledException)
            {
                error.WriteLine($"Unable to load ADR draft template: {exception.Message}");
                return ExitCodes.OperationalError;
            }
        }

        if (injectedProvider is not null)
        {
            WriteProviderSelection(draftArguments, output);

            return RunDraftCommand(
                draftArguments,
                injectedProvider,
                output,
                error,
                selectedTemplate,
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(
                draftArguments.ProviderName)
            || string.IsNullOrWhiteSpace(
                draftArguments.Model))
        {
            error.WriteLine(
                "'draft' requires both --provider and --model.");
            error.WriteLine(
                "Run 'adr-guard draft --help' for usage.");
            return ExitCodes.UsageError;
        }

        WriteProviderSelection(draftArguments, output);

        try
        {
            using var httpClient =
                httpClientFactory?.Invoke()
                ?? new HttpClient();

            var provider =
                AdrGenerationProviderFactory.Create(
                    draftArguments.ProviderName,
                    draftArguments.Model,
                    draftArguments.Endpoint,
                    httpClient,
                    environmentVariableReader);

            return RunDraftCommand(
                draftArguments,
                provider,
                output,
                error,
                selectedTemplate,
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            error.WriteLine(exception.Message);
            error.WriteLine(
                "Run 'adr-guard draft --help' for usage.");
            return ExitCodes.UsageError;
        }
        catch (InvalidOperationException exception)
        {
            error.WriteLine(exception.Message);
            return ExitCodes.OperationalError;
        }
    }

    private static int RunDraftCommand(
        DraftArguments arguments,
        IAdrGenerationProvider provider,
        TextWriter output,
        TextWriter error,
        AdrTemplateDefinition? template,
        CancellationToken cancellationToken) =>
        DraftCommand.Run(
            arguments.DirectoryPath,
            arguments.Title,
            arguments.Context,
            arguments.CultureName,
            arguments.ContextFilePaths,
            arguments.IncludeExistingAdrs,
            arguments.DryRun,
            provider,
            output,
            error,
            template,
            cancellationToken);

    private static void WriteProviderSelection(
        DraftArguments arguments,
        TextWriter output)
    {
        if (!string.IsNullOrWhiteSpace(arguments.ProviderName))
        {
            output.WriteLine($"AI provider: {arguments.ProviderName}");
        }

        if (!string.IsNullOrWhiteSpace(arguments.Model))
        {
            output.WriteLine($"AI model: {arguments.Model}");
        }
    }

    private static bool TryParseIndexArguments(
        IReadOnlyList<string> args,
        out string directoryPath,
        out string? outputPath)
    {
        directoryPath = ".";
        outputPath = null;
        var directoryAssigned = false;

        for (var index = 1; index < args.Count; index++)
        {
            var argument = args[index];

            if (argument == "--output")
            {
                if (outputPath is not null
                    || index + 1 >= args.Count)
                {
                    return false;
                }

                outputPath = args[++index];
                if (string.IsNullOrWhiteSpace(outputPath)
                    || outputPath.StartsWith('-'))
                {
                    return false;
                }

                continue;
            }

            if (argument.StartsWith('-')
                || directoryAssigned)
            {
                return false;
            }

            directoryPath = argument;
            directoryAssigned = true;
        }

        return true;
    }

    private static bool TryParseReviewArguments(
        IReadOnlyList<string> args,
        out ReviewArguments reviewArguments)
    {
        string? targetPath = null;
        string? providerName = null;
        string? model = null;
        string? endpoint = null;
        string? outputPath = null;
        string? policyFilePath = null;
        var contextFilePaths = new List<string>();
        var includeExistingAdrs = false;
        var format = AdrReviewOutputFormat.Text;
        var formatAssigned = false;
        var overwriteOutput = false;
        var policyMode = AdrReviewPolicyMode.Advisory;
        var policyAssigned = false;

        for (var index = 1; index < args.Count; index++)
        {
            var argument = args[index];

            if (argument == "--include-existing-adrs")
            {
                if (includeExistingAdrs)
                {
                    reviewArguments = ReviewArguments.Empty;
                    return false;
                }

                includeExistingAdrs = true;
                continue;
            }

            if (argument == "--overwrite")
            {
                if (overwriteOutput)
                {
                    reviewArguments = ReviewArguments.Empty;
                    return false;
                }

                overwriteOutput = true;
                continue;
            }

            if (argument is
                "--provider"
                or "--model"
                or "--endpoint"
                or "--context-file"
                or "--policy"
                or "--policy-file"
                or "--format"
                or "--output")
            {
                if (index + 1 >= args.Count)
                {
                    reviewArguments = ReviewArguments.Empty;
                    return false;
                }

                var value = args[++index];

                if (string.IsNullOrWhiteSpace(value)
                    || value.StartsWith('-'))
                {
                    reviewArguments = ReviewArguments.Empty;
                    return false;
                }

                switch (argument)
                {
                    case "--provider":
                        if (providerName is not null)
                        {
                            reviewArguments = ReviewArguments.Empty;
                            return false;
                        }

                        providerName = value;
                        break;

                    case "--model":
                        if (model is not null)
                        {
                            reviewArguments = ReviewArguments.Empty;
                            return false;
                        }

                        model = value;
                        break;

                    case "--endpoint":
                        if (endpoint is not null)
                        {
                            reviewArguments = ReviewArguments.Empty;
                            return false;
                        }

                        endpoint = value;
                        break;

                    case "--context-file":
                        contextFilePaths.Add(value);
                        break;

                    case "--policy":
                        if (policyAssigned)
                        {
                            reviewArguments = ReviewArguments.Empty;
                            return false;
                        }

                        policyMode = value switch
                        {
                            "advisory" => AdrReviewPolicyMode.Advisory,
                            "enforce" => AdrReviewPolicyMode.Enforce,
                            _ => (AdrReviewPolicyMode)(-1),
                        };

                        if (!Enum.IsDefined(policyMode))
                        {
                            reviewArguments = ReviewArguments.Empty;
                            return false;
                        }

                        policyAssigned = true;
                        break;

                    case "--policy-file":
                        if (policyFilePath is not null)
                        {
                            reviewArguments = ReviewArguments.Empty;
                            return false;
                        }

                        policyFilePath = value;
                        break;

                    case "--format":
                        if (formatAssigned)
                        {
                            reviewArguments = ReviewArguments.Empty;
                            return false;
                        }

                        format = value switch
                        {
                            "text" => AdrReviewOutputFormat.Text,
                            "json" => AdrReviewOutputFormat.Json,
                            _ => (AdrReviewOutputFormat)(-1),
                        };

                        if (!Enum.IsDefined(format))
                        {
                            reviewArguments = ReviewArguments.Empty;
                            return false;
                        }

                        formatAssigned = true;
                        break;

                    case "--output":
                        if (outputPath is not null)
                        {
                            reviewArguments = ReviewArguments.Empty;
                            return false;
                        }

                        outputPath = value;
                        break;
                }

                continue;
            }

            if (argument.StartsWith('-')
                || targetPath is not null)
            {
                reviewArguments = ReviewArguments.Empty;
                return false;
            }

            targetPath = argument;
        }

        if (overwriteOutput
            && outputPath is null)
        {
            reviewArguments = ReviewArguments.Empty;
            return false;
        }

        reviewArguments = new ReviewArguments(
            targetPath ?? string.Empty,
            providerName,
            model,
            endpoint,
            contextFilePaths,
            includeExistingAdrs,
            format,
            outputPath,
            overwriteOutput,
            policyMode,
            policyFilePath);

        return !string.IsNullOrWhiteSpace(targetPath);
    }

    private static bool TryParseDraftArguments(
        IReadOnlyList<string> args,
        out DraftArguments draftArguments)
    {
        var directoryPath = ".";
        var title = string.Empty;
        var context = string.Empty;
        var cultureName = DefaultDraftCultureName;
        string? providerName = null;
        string? model = null;
        string? endpoint = null;
        string? templateName = null;
        string? templateFilePath = null;
        var contextFilePaths = new List<string>();
        var includeExistingAdrs = false;
        var dryRun = false;

        var directoryAssigned = false;
        var titleAssigned = false;
        var contextAssigned = false;
        var cultureAssigned = false;
        var providerAssigned = false;
        var modelAssigned = false;
        var endpointAssigned = false;
        var templateAssigned = false;
        var templateFileAssigned = false;
        var includeExistingAdrsAssigned = false;
        var dryRunAssigned = false;

        for (var index = 1; index < args.Count; index++)
        {
            var argument = args[index];

            if (argument == "--include-existing-adrs")
            {
                if (includeExistingAdrsAssigned)
                {
                    draftArguments = DraftArguments.Empty;
                    return false;
                }

                includeExistingAdrs = true;
                includeExistingAdrsAssigned = true;
                continue;
            }

            if (argument is "--dry-run" or "--preview")
            {
                if (dryRunAssigned)
                {
                    draftArguments = DraftArguments.Empty;
                    return false;
                }

                dryRun = true;
                dryRunAssigned = true;
                continue;
            }

            if (argument is "--title"
                or "--context"
                or "--culture"
                or "--provider"
                or "--model"
                or "--endpoint"
                or "--context-file"
                or "--template"
                or "--template-file")
            {
                if (index + 1 >= args.Count)
                {
                    draftArguments = DraftArguments.Empty;
                    return false;
                }

                var value = args[++index];
                if (string.IsNullOrWhiteSpace(value)
                    || value.StartsWith('-'))
                {
                    draftArguments = DraftArguments.Empty;
                    return false;
                }

                switch (argument)
                {
                    case "--title":
                        if (titleAssigned)
                        {
                            draftArguments = DraftArguments.Empty;
                            return false;
                        }

                        title = value;
                        titleAssigned = true;
                        break;

                    case "--context":
                        if (contextAssigned)
                        {
                            draftArguments = DraftArguments.Empty;
                            return false;
                        }

                        context = value;
                        contextAssigned = true;
                        break;

                    case "--culture":
                        if (cultureAssigned)
                        {
                            draftArguments = DraftArguments.Empty;
                            return false;
                        }

                        cultureName = value;
                        cultureAssigned = true;
                        break;

                    case "--provider":
                        if (providerAssigned)
                        {
                            draftArguments = DraftArguments.Empty;
                            return false;
                        }

                        providerName = value;
                        providerAssigned = true;
                        break;

                    case "--model":
                        if (modelAssigned)
                        {
                            draftArguments = DraftArguments.Empty;
                            return false;
                        }

                        model = value;
                        modelAssigned = true;
                        break;

                    case "--endpoint":
                        if (endpointAssigned)
                        {
                            draftArguments = DraftArguments.Empty;
                            return false;
                        }

                        endpoint = value;
                        endpointAssigned = true;
                        break;

                    case "--context-file":
                        contextFilePaths.Add(value);
                        break;

                    case "--template":
                        if (templateAssigned)
                        {
                            draftArguments = DraftArguments.Empty;
                            return false;
                        }

                        templateName = value;
                        templateAssigned = true;
                        break;

                    case "--template-file":
                        if (templateFileAssigned)
                        {
                            draftArguments = DraftArguments.Empty;
                            return false;
                        }

                        templateFilePath = value;
                        templateFileAssigned = true;
                        break;
                }

                continue;
            }

            if (argument.StartsWith('-')
                || directoryAssigned)
            {
                draftArguments = DraftArguments.Empty;
                return false;
            }

            directoryPath = argument;
            directoryAssigned = true;
        }

        draftArguments = new DraftArguments(
            directoryPath,
            title,
            context,
            cultureName,
            providerName,
            model,
            endpoint,
            contextFilePaths,
            includeExistingAdrs,
            dryRun,
            templateName,
            templateFilePath);

        return titleAssigned && contextAssigned;
    }

    private static int WriteUsageError(
        IReadOnlyList<string> args,
        TextWriter error)
    {
        error.WriteLine(
            $"Unknown argument or command: '{string.Join(' ', args)}'.");
        error.WriteLine(
            "Run 'adr-guard --help' for usage.");

        return ExitCodes.UsageError;
    }

    private static int WriteCommandUsageError(
        string command,
        TextWriter error)
    {
        error.WriteLine(
            $"Invalid arguments for '{command}'.");
        error.WriteLine(
            $"Run 'adr-guard {command} --help' for usage.");

        return ExitCodes.UsageError;
    }

    private static bool IsHelpRequest(
        IReadOnlyList<string> args) =>
        args.Count == 1
        && IsHelpOption(args[0]);

    private static bool IsHelpOption(
        string value) =>
        value is "-h" or "--help";

    private static bool IsVersionRequest(
        IReadOnlyList<string> args) =>
        args.Count == 1
        && args[0] == "--version";

    private static string GetVersion()
    {
        var assembly = typeof(CliApplication).Assembly;

        return assembly
                   .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                   ?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? "unknown";
    }

    private sealed record ReviewArguments(
        string TargetPath,
        string? ProviderName,
        string? Model,
        string? Endpoint,
        IReadOnlyList<string> ContextFilePaths,
        bool IncludeExistingAdrs,
        AdrReviewOutputFormat Format,
        string? OutputPath,
        bool OverwriteOutput,
        AdrReviewPolicyMode PolicyMode,
        string? PolicyFilePath)
    {
        internal static ReviewArguments Empty { get; } =
            new(
                string.Empty,
                null,
                null,
                null,
                [],
                false,
                AdrReviewOutputFormat.Text,
                null,
                false,
                AdrReviewPolicyMode.Advisory,
                null);
    }

    private sealed record DraftArguments(
        string DirectoryPath,
        string Title,
        string Context,
        string CultureName,
        string? ProviderName,
        string? Model,
        string? Endpoint,
        IReadOnlyList<string> ContextFilePaths,
        bool IncludeExistingAdrs,
        bool DryRun,
        string? TemplateName,
        string? TemplateFilePath)
    {
        internal static DraftArguments Empty { get; } =
            new(
                ".",
                string.Empty,
                string.Empty,
                DefaultDraftCultureName,
                null,
                null,
                null,
                [],
                false,
                false,
                null,
                null);
    }
}
