namespace AdrGuard.Agent;

internal static class AgentApplication
{
    private const int SuccessExitCode = 0;
    private const int UsageErrorExitCode = 2;
    private const int OperationalErrorExitCode = 3;

    private const string HelpText = """
        ADR Guard Agent

        Development-only architectural workflow over ADR Guard MCP and authorized ADR Guard Agent Skills.
        ADR proposals are previewed before an optional, independently authorized create operation.
        Model output is advisory and never approves a decision or establishes compliance.

        Usage:
          adr-guard-agent --repository <path> --adr-directory <relative-path> --mcp-command <path-or-name> --question <text> [options]
          adr-guard-agent --help

        Required options:
          --repository <path>      Existing repository root selected explicitly by the user.
          --adr-directory <path>   Existing ADR directory relative to the selected repository.
          --mcp-command <command>  Explicit executable or command used to start ADR Guard MCP.
          --question <text>       Architectural question, limited to 4096 characters.

        Options:
          -h, --help               Show command-line help.
          --mcp-argument <value>   Discrete argument before MCP repository options; repeat as needed.
          --mcp-timeout-seconds <1-300>
                                   Initialization and per-tool timeout. Default: 30.
          --cli-command <command>  Explicit ADR Guard CLI executable for proposal preview and supervised creation.
          --cli-argument <value>   Discrete argument before the CLI command; repeat as needed.
          --cli-timeout-seconds <1-300>
                                   Per-operation proposal CLI timeout. Default: 30.
          --enable-adr-creation    Enable the interactive, fingerprint-bound creation prompt.
                                   Redirected/non-interactive input cannot authorize creation.
          --provider openai       Optional language-model provider; requires --model.
          --model <model>          Optional provider model identifier; requires --provider.
          --allow-remote-context  Required with a provider; authorizes sending selected context remotely.
          --max-iterations <1-10>  Provider iterations. The first flow uses one. Default: 1.
          --max-context-characters <1024-1048576>
                                   Maximum question plus evidence context. Default: 65536.
          --max-output-tokens <1-16384>
                                   Maximum provider output tokens. Default: 2048.
          --max-duration-seconds <1-600>
                                   Total execution duration. Default: 120.
          --max-tool-calls <1-20>  Maximum read-only MCP calls. Default: 4.
          --search-limit <1-100>   Maximum ADR search results. Default: 20.
          --impact-manifest <path> Explicit repository-relative impact mapping.
          --impact-base-reference <ref>
                                   Explicit Git base for impact. Default: HEAD.
          --architecture-rules <path>
                                   Explicit repository-relative fitness rules.
          --architecture-evidence <path>
                                   Existing bounded evidence; repeat as needed.
          --architecture-baseline <path>
                                   Existing read-only drift baseline.
          --architecture-policy <advisory|enforce>
                                   Deterministic MCP policy. Default: advisory.

        Credentials:
          OPENAI_API_KEY           Required environment variable for provider 'openai'.

        Exit codes:
          0  Success
          2  Invalid command-line usage
          3  Operational error or cancellation
        """;

    internal static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        IAgentRunner? runner = null,
        TextReader? input = null,
        bool? isInteractive = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Count == 1 && args[0] is "-h" or "--help")
        {
            await output.WriteLineAsync(HelpText).ConfigureAwait(false);
            return SuccessExitCode;
        }

        AgentConfiguration configuration;
        try
        {
            configuration = AgentConfiguration.Parse(args);
        }
        catch (AgentConfigurationException exception)
        {
            await error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            await error.WriteLineAsync("Run 'adr-guard-agent --help' for usage.").ConfigureAwait(false);
            return UsageErrorExitCode;
        }

        runner ??= new ConfiguredArchitectureQuestionRunner(
            new ConsoleAdrProposalAuthorization(
                input ?? CancelableConsoleReader.Instance,
                output,
                isInteractive ?? !Console.IsInputRedirected));
        try
        {
            var result = await runner.RunAsync(configuration, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(result.Message))
            {
                await output.WriteLineAsync(result.Message).ConfigureAwait(false);
            }

            return SuccessExitCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync("Agent execution was canceled.").ConfigureAwait(false);
            return OperationalErrorExitCode;
        }
        catch (AgentExecutionException exception)
        {
            await error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return OperationalErrorExitCode;
        }
    }

}
