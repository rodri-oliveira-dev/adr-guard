using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AdrGuard.Agent;

internal static class AdrCreationProposalFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    internal static AdrCreationProposal Create(
        AgentConfiguration configuration,
        AdrFormDescription description,
        AdrProposal proposal,
        AdrFormPreview preview)
    {
        var cli = configuration.AdrProposal
            ?? throw new AgentCliException("ADR creation requires an explicitly configured CLI.");
        var fields = proposal.Fields
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        var arguments = cli.CliArguments
            .Concat(["new", configuration.AdrDirectory, "--form"])
            .ToArray();
        var fingerprint = ComputeFingerprint(new FingerprintMaterial(
            configuration.AdrDirectory,
            cli.CliCommand,
            arguments,
            cli.CliTimeout,
            description.Template,
            proposal.Title,
            fields,
            preview.Markdown));

        return new AdrCreationProposal(
            fingerprint,
            configuration.AdrDirectory,
            cli.CliCommand,
            arguments,
            description.Template,
            proposal.Title,
            fields,
            preview);
    }

    internal static bool MatchesConfigurationAndFingerprint(
        AgentConfiguration configuration,
        AdrCreationProposal proposal)
    {
        var cli = configuration.AdrProposal;
        if (cli is null)
        {
            return false;
        }

        var expectedArguments = cli.CliArguments
            .Concat(["new", configuration.AdrDirectory, "--form"])
            .ToArray();
        return string.Equals(proposal.TargetDirectory, configuration.AdrDirectory, StringComparison.Ordinal)
            && string.Equals(proposal.CliCommand, cli.CliCommand, StringComparison.Ordinal)
            && proposal.CliArguments.SequenceEqual(expectedArguments, StringComparer.Ordinal)
            && string.Equals(
                proposal.Fingerprint,
                ComputeFingerprint(new FingerprintMaterial(
                    proposal.TargetDirectory,
                    proposal.CliCommand,
                    proposal.CliArguments,
                    cli.CliTimeout,
                    proposal.Template,
                    proposal.Title,
                    proposal.Fields,
                    proposal.Preview.Markdown)),
                StringComparison.Ordinal);
    }

    private static string ComputeFingerprint(FingerprintMaterial material)
    {
        var orderedFields = material.Fields
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        var fingerprintInput = JsonSerializer.Serialize(
            new
            {
                schemaVersion = "1.0",
                operation = "create",
                targetDirectory = material.TargetDirectory,
                cliCommand = material.CliCommand,
                cliArguments = material.CliArguments,
                cliTimeoutTicks = material.CliTimeout.Ticks,
                template = material.Template,
                title = material.Title,
                fields = orderedFields,
                markdown = material.Markdown,
            },
            JsonOptions);
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintInput)));
    }

    private sealed record FingerprintMaterial(
        string TargetDirectory,
        string CliCommand,
        IReadOnlyList<string> CliArguments,
        TimeSpan CliTimeout,
        string Template,
        string Title,
        IReadOnlyDictionary<string, string> Fields,
        string Markdown);
}

internal sealed class ConsoleAdrProposalAuthorization(
    TextReader input,
    TextWriter output,
    bool isInteractive) : IAdrProposalAuthorization
{
    public async Task<AdrProposalAuthorizationDecision> AuthorizeAsync(
        AdrCreationProposal proposal,
        string preview,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentException.ThrowIfNullOrWhiteSpace(preview);

        await output.WriteLineAsync(preview).ConfigureAwait(false);
        if (!isInteractive)
        {
            throw new AgentCliException(
                "ADR creation was rejected because non-interactive input cannot provide verifiable human authorization.");
        }

        await output.WriteLineAsync().ConfigureAwait(false);
        await output.WriteLineAsync(
            $"To create this exact Proposed ADR, type: create {proposal.Fingerprint}").ConfigureAwait(false);
        await output.WriteLineAsync("Type 'deny' to refuse or 'cancel' to cancel.").ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var readTask = input.ReadLineAsync(cancellationToken).AsTask();
        var response = await readTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (response is null || string.Equals(response.Trim(), "cancel", StringComparison.OrdinalIgnoreCase))
        {
            return AdrProposalAuthorizationDecision.Canceled;
        }
        if (string.Equals(
            response.Trim(),
            $"create {proposal.Fingerprint}",
            StringComparison.Ordinal))
        {
            return AdrProposalAuthorizationDecision.Approved;
        }

        return AdrProposalAuthorizationDecision.Denied;
    }
}

internal sealed class CancelableConsoleReader : TextReader
{
    internal static CancelableConsoleReader Instance { get; } = new();

    private CancelableConsoleReader()
    {
    }

    public override ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken) =>
        new(ReadConsoleLineAsync(cancellationToken));

    private static async Task<string?> ReadConsoleLineAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var readTask = Task.Run(static () => Console.ReadLine(), CancellationToken.None);
        return await readTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}
