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
        var fingerprint = ComputeFingerprint(
            configuration.AdrDirectory,
            cli.CliCommand,
            arguments,
            cli.CliTimeout,
            description.Template,
            proposal.Title,
            fields,
            preview.Markdown);

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
                ComputeFingerprint(
                    proposal.TargetDirectory,
                    proposal.CliCommand,
                    proposal.CliArguments,
                    cli.CliTimeout,
                    proposal.Template,
                    proposal.Title,
                    proposal.Fields,
                    proposal.Preview.Markdown),
                StringComparison.Ordinal);
    }

    private static string ComputeFingerprint(
        string targetDirectory,
        string cliCommand,
        IReadOnlyList<string> cliArguments,
        TimeSpan cliTimeout,
        string template,
        string title,
        IReadOnlyDictionary<string, string> fields,
        string markdown)
    {
        var orderedFields = fields
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        var fingerprintInput = JsonSerializer.Serialize(
            new
            {
                schemaVersion = "1.0",
                operation = "create",
                targetDirectory,
                cliCommand,
                cliArguments,
                cliTimeoutTicks = cliTimeout.Ticks,
                template,
                title,
                fields = orderedFields,
                markdown,
            },
            JsonOptions);
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintInput)));
    }
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
        var response = await input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
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
