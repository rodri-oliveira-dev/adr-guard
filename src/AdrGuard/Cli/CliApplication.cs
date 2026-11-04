using System.Reflection;
using AdrGuard.Configuration;
using AdrGuard.Drift;
using AdrGuard.Generation;
using AdrGuard.Generation.Providers;
using AdrGuard.Git;
using AdrGuard.Impact;
using AdrGuard.Review;
using AdrGuard.Review.Policy;
using AdrGuard.Review.Providers;
using AdrGuard.Review.Reporting;
using AdrGuard.Review.Security;
using AdrGuard.Validation;

namespace AdrGuard.Cli;

internal static class CliApplication
{
    private const string DraftUsageHint =
        "Run 'adr-guard draft --help' for usage.";

    private const string HelpText = """
        ADR Guard

        Validate and maintain Architecture Decision Records from the command line.

        Usage:
          adr-guard init [repository] [options]
          adr-guard check [directory] [--format text|json|sarif] [--diagnostic-locations] [--adr-format canonical|madr-4] [--lifecycle-statuses <mapping>] [--conventional-supersession] [--filename-policy <policy>] [--placeholder-policy off|warn|error] [--validate-metadata] [--validation-profile <profile>] [--changed --base-ref <ref>] [--baseline <file>]
          adr-guard baseline [directory] --output <file> [--update] [--adr-format canonical|madr-4] [--lifecycle-statuses <mapping>] [--conventional-supersession] [--filename-policy <policy>] [--validation-profile <profile>]
          adr-guard index [directory] [--output <file>] [--adr-format canonical|madr-4] [--lifecycle-statuses <mapping>] [--conventional-supersession] [--filename-policy <policy>] [--validation-profile <profile>] [--catalog enriched]
          adr-guard new [adr-directory] (--title <title> [--dry-run|--preview] | --form) [--template minimal|extended] [--template-file <path>] [--culture en-US|pt-BR] [--filename-policy <policy>]
          adr-guard edit <adr-file> --form [--adr-format canonical|madr-4] [--lifecycle-statuses <mapping>] [--conventional-supersession] [--filename-policy <policy>] [--placeholder-policy off|warn|error] [--validate-metadata] [--validation-profile <profile>]
          adr-guard draft [directory] --title <title> --context <context> --provider <provider> --model <model> [--culture <name>] [--template minimal|extended | --template-file <path>] [--endpoint <uri>] [--context-file <path>]... [--include-existing-adrs] [--dry-run|--preview]
          adr-guard review <adr-file> --provider <provider> --model <model> [--compare-ref <ref>] [--endpoint <uri>] [--context-file <path>]... [--include-existing-adrs] [--policy advisory|enforce] [--policy-file <path>] [--format text|json] [--output <path> [--overwrite]]
          adr-guard impact [repository] --base-ref <ref> --map <file> [--format text|json]
          adr-guard drift [repository] --rules <file> [--evidence <file>]... [--baseline <file>] [--write-baseline <file> [--update-baseline]] [--format text|json] [--policy advisory|enforce]
          adr-guard [options]

        Commands:
          init     Initialize ADR Guard configuration in an existing repository.
          check    Validate ADR files. Defaults to the current directory.
          baseline Generate or explicitly update a versioned diagnostic baseline.
          index    Validate ADR files and generate an index. Defaults to README.md.
          new      Create a Proposed ADR from an offline Markdown template.
          edit     Safely load or update one existing ADR through a versioned form contract.
          draft    Generate a Proposed ADR draft through a configured AI provider.
          review   Request an advisory, read-only technical review of one existing ADR.
          impact   Correlate Git changes with explicit ADR-to-code mappings.
          drift    Verify ADR-backed architecture fitness rules from bounded evidence.

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

    private const string ImpactHelpText = """
        Usage:
          adr-guard impact [repository] --base-ref <ref> --map <file> [--format text|json]

        Correlate committed, staged, unstaged and untracked changes with explicit ADR mappings.
        The repository defaults to the current directory. The map is a repository-relative JSON file.
        The base reference is explicit; ADR Guard does not fetch, checkout, write files or alter Git state.
        Text is advisory. JSON is a deterministic schemaVersion 1.0 document written only to stdout.
        Paths, evidence and reasons originate from local Git data and the bounded mapping manifest;
        treat report content as untrusted when forwarding it to logs or other systems.

        Exit codes:
          0  Analysis completed, including when decisions are affected or coverage is incomplete
          2  Invalid command-line usage
          3  Git, manifest, repository or cancellation failure
        """;

    private const string DriftHelpText = """
        Usage:
          adr-guard drift [repository] --rules <file> [--evidence <file>]... [--baseline <file>] [--write-baseline <file> [--update-baseline]] [--format text|json] [--policy advisory|enforce]

        Verify explicit ADR-backed fitness rules from local bounded evidence. Advisory is the default.
        Unknown evidence returns exit 3 and is never reported as compliance. Enforce returns exit 4 only
        for a new, gate-enabled, deterministic static violation. Reported external failures never block CI.
        Existing baselined violations remain visible; only an explicit baseline create/update changes history.

        Exit codes:
          0  Conclusive advisory result, or enforce result without a new deterministic violation
          2  Invalid command-line usage
          3  Unknown/inconclusive evidence, malformed input, repository error or cancellation
          4  New gate-enabled deterministic violation under --policy enforce
        """;

    private const string CheckHelpText = """
        Usage:
          adr-guard check [directory] [--format text|json|sarif] [--diagnostic-locations] [--adr-format canonical|madr-4] [--lifecycle-statuses <mapping>] [--conventional-supersession] [--filename-policy <policy>] [--placeholder-policy off|warn|error] [--validate-metadata] [--validation-profile <profile>] [--changed --base-ref <ref>] [--baseline <file>]

        Validate ADR files recursively. The directory defaults to the configured ADR directory or current directory.
        Text is the default. JSON and SARIF 2.1.0 are deterministic data documents written to stdout.
        --diagnostic-locations opts machine reports into evidenced one-based line and column locations.
        ADR format defaults to canonical; MADR 4.0 is selected explicitly with --adr-format madr-4.
        Extra lifecycle values are opt-in mappings such as 'Rejected=rejected,Under Review=proposed'.
        Filename policies are canonical, unnumbered, adr-prefix, or numeric:1..9.
        Validation profiles are legacy, advisory, standard, or strict; legacy is the compatible default.
        Placeholder and metadata validation are explicit opt-ins and do not change legacy defaults.
        --changed is opt-in and requires --base-ref. Git failures or insufficient history are operational errors.
        --baseline classifies new/existing/resolved diagnostics; global integrity diagnostics cannot be suppressed.
        """;

    private const string IndexHelpText = """
        Usage:
          adr-guard index [directory] [--output <file>] [--adr-format canonical|madr-4] [--lifecycle-statuses <mapping>] [--conventional-supersession] [--filename-policy <policy>] [--validation-profile <profile>] [--catalog enriched]

        Validate ADR files and generate a Markdown index.
        The directory defaults to the current directory.
        The output defaults to README.md inside the ADR directory.
        Relative --output paths are resolved from the current working directory.
        --catalog enriched opts in to the deterministic governance catalog; default output is unchanged.
        Filename policies are canonical, unnumbered, adr-prefix, or numeric:1..9.
        Validation profiles are legacy, advisory, standard, or strict; legacy is the compatible default.
        """;

    private const string BaselineHelpText = """
        Usage:
          adr-guard baseline [directory] --output <file.json> [--update] [--adr-format canonical|madr-4] [--lifecycle-statuses <mapping>] [--conventional-supersession] [--filename-policy <policy>] [--validation-profile <profile>]

        Explicitly generate a versioned diagnostic baseline. Existing output is replaced only with --update.
        Baselines classify structural debt but never suppress global integrity or operational failures.
        Filename policies are canonical, unnumbered, adr-prefix, or numeric:1..9.
        Validation profiles are legacy, advisory, standard, or strict; legacy is the compatible default.
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
          adr-guard review <adr-file> --provider <provider> --model <model> [--compare-ref <ref>] [--endpoint <uri>] [--context-file <path>]... [--include-existing-adrs] [--policy advisory|enforce] [--policy-file <path>] [--format text|json] [--output <path> [--overwrite]]

        Request an AI-assisted technical review of one existing, structurally valid ADR.
        The command is advisory and read-only: it does not edit the ADR, change its status,
        update an index, accept/reject the decision, or alter git state.

        Required provider options:
          --provider <provider>   openai | anthropic | gemini | openai-compatible
          --model <model>         Provider model identifier. ADR Guard does not choose a default model.

        Optional options:
          --compare-ref <ref>     Explicitly compare the current ADR with the same path at one Git reference.
                                  Only those two versions are selected; repository history is not transmitted.
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
            TextReader.Null,
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
            TextReader.Null,
            output,
            error,
            generationProvider,
            httpClientFactory,
            environmentVariableReader,
            reviewProvider,
            cancellationToken);

    internal static int Run(
        IReadOnlyList<string> args,
        TextReader input,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default) =>
        RunCore(
            args,
            input,
            output,
            error,
            null,
            null,
            null,
            null,
            cancellationToken);

    private static int RunCore(
        IReadOnlyList<string> args,
        TextReader input,
        TextWriter output,
        TextWriter error,
        IAdrGenerationProvider? generationProvider,
        Func<HttpClient>? httpClientFactory,
        Func<string, string?>? environmentVariableReader,
        IAdrReviewProvider? reviewProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(input);
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

        IReadOnlyList<string> effectiveArgs;
        try
        {
            var configuration = args[0] == "init"
                ? null
                : AdrGuardConfigurationLoader.Load(Directory.GetCurrentDirectory());
            effectiveArgs = ConfigurationArguments.Apply(args, configuration);
        }
        catch (AdrGuardConfigurationException exception)
        {
            error.WriteLine(exception.Message);
            return ExitCodes.UsageError;
        }

        return effectiveArgs[0] switch
        {
            "init" => InitCommand.Run(effectiveArgs, output, error),
            "check" => RunCheck(effectiveArgs, output, error, cancellationToken),
            "baseline" => RunBaseline(effectiveArgs, output, error, cancellationToken),
            "index" => RunIndex(effectiveArgs, output, error),
            "new" => NewCommand.Run(effectiveArgs, input, output, error, cancellationToken),
            "edit" => EditFormCommand.Run(effectiveArgs, input, output, error, cancellationToken),
            "draft" => RunDraft(
                effectiveArgs,
                output,
                error,
                generationProvider,
                httpClientFactory,
                environmentVariableReader,
                cancellationToken),
            "review" => RunReview(
                effectiveArgs,
                output,
                error,
                reviewProvider,
                httpClientFactory,
                environmentVariableReader,
                cancellationToken),
            "impact" => RunImpact(effectiveArgs, output, error, cancellationToken),
            "drift" => RunDrift(effectiveArgs, output, error, cancellationToken),
            _ => WriteUsageError(effectiveArgs, error),
        };
    }

    private static int RunDrift(IReadOnlyList<string> args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (args.Count == 2 && IsHelpOption(args[1])) { output.WriteLine(DriftHelpText); return ExitCodes.Success; }
        if (!TryParseDriftArguments(args, out var repository, out var rules, out var evidence, out var baseline, out var writeBaseline, out var updateBaseline, out var format, out var policy))
            return WriteCommandUsageError("drift", error);
        return DriftCommand.Run(repository, rules, evidence, baseline, writeBaseline, updateBaseline, format, policy, output, error, cancellationToken);
    }

    private static bool TryParseDriftArguments(IReadOnlyList<string> args, out string repository, out string rules, out IReadOnlyList<string> evidence,
        out string? baseline, out string? writeBaseline, out bool updateBaseline, out DriftOutputFormat format, out DriftPolicy policy)
    {
        repository = Directory.GetCurrentDirectory(); rules = string.Empty; baseline = null; writeBaseline = null; updateBaseline = false;
        format = DriftOutputFormat.Text; policy = DriftPolicy.Advisory; var files = new List<string>(); evidence = files;
        var repositoryAssigned = false; var assigned = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument == "--update-baseline") { if (updateBaseline) return false; updateBaseline = true; continue; }
            if (argument is "--rules" or "--evidence" or "--baseline" or "--write-baseline" or "--format" or "--policy")
            {
                if (index + 1 >= args.Count) return false;
                var value = args[++index]; if (string.IsNullOrWhiteSpace(value) || value.StartsWith('-')) return false;
                if (argument != "--evidence" && !assigned.Add(argument)) return false;
                switch (argument)
                {
                    case "--rules": rules = value; break;
                    case "--evidence": files.Add(value); break;
                    case "--baseline": baseline = value; break;
                    case "--write-baseline": writeBaseline = value; break;
                    case "--format": if (value is not ("text" or "json")) return false; format = value == "json" ? DriftOutputFormat.Json : DriftOutputFormat.Text; break;
                    case "--policy": if (value is not ("advisory" or "enforce")) return false; policy = value == "enforce" ? DriftPolicy.Enforce : DriftPolicy.Advisory; break;
                }
                continue;
            }
            if (argument.StartsWith('-') || repositoryAssigned) return false;
            repository = argument; repositoryAssigned = true;
        }
        return rules.Length > 0 && (!updateBaseline || writeBaseline is not null) && files.Count <= ExternalFitnessEvidenceAdapter.MaximumFiles;
    }

    private static int RunImpact(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (args.Count == 2 && IsHelpOption(args[1]))
        {
            output.WriteLine(ImpactHelpText);
            return ExitCodes.Success;
        }

        if (!TryParseImpactArguments(
                args,
                out var repositoryPath,
                out var baseReference,
                out var manifestPath,
                out var format))
        {
            return WriteCommandUsageError("impact", error);
        }

        return ImpactCommand.Run(
            repositoryPath,
            baseReference,
            manifestPath,
            format,
            output,
            error,
            cancellationToken);
    }

    private static bool TryParseImpactArguments(
        IReadOnlyList<string> args,
        out string repositoryPath,
        out string baseReference,
        out string manifestPath,
        out ImpactOutputFormat format)
    {
        repositoryPath = Directory.GetCurrentDirectory();
        baseReference = string.Empty;
        manifestPath = string.Empty;
        format = ImpactOutputFormat.Text;
        var repositoryAssigned = false;
        var baseAssigned = false;
        var mapAssigned = false;
        var formatAssigned = false;

        for (var index = 1; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument is "--base-ref" or "--map" or "--format")
            {
                if (index + 1 >= args.Count)
                {
                    return false;
                }

                var value = args[++index];
                if (string.IsNullOrWhiteSpace(value) || value.StartsWith('-'))
                {
                    return false;
                }

                if (argument == "--base-ref")
                {
                    if (baseAssigned)
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

                    baseReference = value;
                    baseAssigned = true;
                }
                else if (argument == "--map")
                {
                    if (mapAssigned)
                    {
                        return false;
                    }

                    manifestPath = value;
                    mapAssigned = true;
                }
                else
                {
                    if (formatAssigned)
                    {
                        return false;
                    }

                    format = value switch
                    {
                        "text" => ImpactOutputFormat.Text,
                        "json" => ImpactOutputFormat.Json,
                        _ => (ImpactOutputFormat)(-1),
                    };
                    if (!Enum.IsDefined(format))
                    {
                        return false;
                    }

                    formatAssigned = true;
                }

                continue;
            }

            if (argument.StartsWith('-') || repositoryAssigned)
            {
                return false;
            }

            repositoryPath = argument;
            repositoryAssigned = true;
        }

        return baseAssigned && mapAssigned;
    }

    private static int RunCheck(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (args.Count == 2 && IsHelpOption(args[1]))
        {
            output.WriteLine(CheckHelpText);
            return ExitCodes.Success;
        }

        if (!CheckCommandArguments.TryParse(args, out var arguments))
        {
            return WriteCommandUsageError("check", error);
        }

        return CheckCommand.Run(
            arguments.DirectoryPath,
            arguments.Format,
            arguments.AdrFormat,
            arguments.LifecycleStatuses,
            arguments.ConventionalSupersession,
            arguments.FilenamePolicy,
            arguments.PlaceholderPolicy,
            arguments.ValidateMetadata,
            arguments.ValidationProfile,
            arguments.Changed,
            arguments.BaseReference,
            arguments.BaselinePath,
            arguments.DiagnosticLocations,
            output,
            error,
            cancellationToken);
    }

    private static int RunBaseline(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (args.Count == 2 && IsHelpOption(args[1]))
        {
            output.WriteLine(BaselineHelpText);
            return ExitCodes.Success;
        }

        if (!BaselineCommandArguments.TryParse(args, out var arguments))
        {
            return WriteCommandUsageError("baseline", error);
        }

        return BaselineCommand.Run(
            arguments.DirectoryPath,
            arguments.OutputPath,
            arguments.Update,
            arguments.Format,
            arguments.LifecycleStatuses,
            arguments.ConventionalSupersession,
            arguments.FilenamePolicy,
            arguments.ValidationProfile,
            output,
            error,
            cancellationToken);
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

        if (!IndexCommandArguments.TryParse(
                args,
                out var indexArguments))
        {
            return WriteCommandUsageError("index", error);
        }

        return IndexCommand.Run(
            indexArguments.DirectoryPath,
            indexArguments.OutputPath,
            indexArguments.AdrFormat,
            indexArguments.LifecycleStatuses,
            indexArguments.ConventionalSupersession,
            indexArguments.FilenamePolicy,
            indexArguments.ValidationProfile,
            indexArguments.EnrichedCatalog,
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

        if (!ReviewCommandArguments.TryParse(args, out var reviewArguments))
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
                    reviewArguments.CompareReference,
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
                    reviewArguments.CompareReference,
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

        if (!DraftCommandArguments.TryParse(
                args,
                out var draftArguments))
        {
            return WriteCommandUsageError(
                "draft",
                error);
        }

        if (!TryResolveDraftTemplate(
                draftArguments,
                error,
                cancellationToken,
                out var selectedTemplate,
                out var templateExitCode))
        {
            return templateExitCode;
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
                DraftUsageHint);
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
                DraftUsageHint);
            return ExitCodes.UsageError;
        }
        catch (InvalidOperationException exception)
        {
            error.WriteLine(exception.Message);
            return ExitCodes.OperationalError;
        }
    }

    private static bool TryResolveDraftTemplate(
        DraftCommandArguments arguments,
        TextWriter error,
        CancellationToken cancellationToken,
        out AdrTemplateDefinition? template,
        out int exitCode)
    {
        template = null;
        exitCode = ExitCodes.Success;

        if (arguments.TemplateName is null
            && arguments.TemplateFilePath is null)
        {
            return true;
        }

        if (!ValidateDraftTemplateSelection(arguments, error))
        {
            exitCode = ExitCodes.UsageError;
            return false;
        }

        try
        {
            var cultureName = string.Equals(
                arguments.CultureName,
                "pt-BR",
                StringComparison.OrdinalIgnoreCase)
                ? "pt-BR"
                : "en-US";

            template = AdrTemplateSelection.Resolve(
                arguments.TemplateName,
                arguments.TemplateFilePath,
                cultureName,
                cancellationToken: cancellationToken);
            return true;
        }
        catch (ArgumentException exception)
        {
            error.WriteLine(exception.Message);
            error.WriteLine(DraftUsageHint);
            exitCode = ExitCodes.UsageError;
            return false;
        }
        catch (Exception exception) when (exception is InvalidDataException
            or IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or OperationCanceledException)
        {
            error.WriteLine($"Unable to load ADR draft template: {exception.Message}");
            exitCode = ExitCodes.OperationalError;
            return false;
        }
    }

    private static bool ValidateDraftTemplateSelection(
        DraftCommandArguments arguments,
        TextWriter error)
    {
        if (arguments.TemplateName is not null
            && arguments.TemplateFilePath is not null)
        {
            error.WriteLine("--template and --template-file are mutually exclusive.");
            error.WriteLine(DraftUsageHint);
            return false;
        }

        if (!string.Equals(arguments.CultureName, "en-US", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(arguments.CultureName, "pt-BR", StringComparison.OrdinalIgnoreCase))
        {
            error.WriteLine("Selected templates support --culture en-US or pt-BR only; unselected draft retains all supported .NET cultures.");
            error.WriteLine(DraftUsageHint);
            return false;
        }

        if (arguments.TemplateName is not null
            && !AdrBuiltInTemplates.Names.Contains(arguments.TemplateName, StringComparer.Ordinal))
        {
            error.WriteLine($"Unknown template '{arguments.TemplateName}'. Use minimal or extended.");
            error.WriteLine(DraftUsageHint);
            return false;
        }

        return true;
    }

    private static int RunDraftCommand(
        DraftCommandArguments arguments,
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
        DraftCommandArguments arguments,
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

}
