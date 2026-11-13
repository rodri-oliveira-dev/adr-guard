using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace AdrGuard.Agent;

internal sealed class AdrNewFormPreviewClient : IAdrProposalPreviewClient
{
    private const string SchemaVersion = "1.0";
    private const int MaximumOutputCharacters = 1_048_576;
    private const int MaximumErrorCharacters = 16_384;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        WriteIndented = false,
    };

    private readonly AgentConfiguration configuration;
    private readonly AdrProposalConfiguration proposalConfiguration;

    internal AdrNewFormPreviewClient(AgentConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        this.configuration = configuration;
        proposalConfiguration = configuration.AdrProposal
            ?? throw new AgentCliException(
                "ADR proposal operations require an explicitly configured --cli-command.");
    }

    public async Task<AdrFormDescription> DescribeAsync(CancellationToken cancellationToken)
    {
        var response = await InvokeAsync(
            JsonSerializer.Serialize(
                new { schemaVersion = SchemaVersion, operation = "describe" },
                JsonOptions),
            "describe",
            cancellationToken).ConfigureAwait(false);
        return ParseDescription(response);
    }

    public async Task<AdrFormPreview> PreviewAsync(
        string title,
        IReadOnlyDictionary<string, string> fields,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(fields);

        var response = await InvokeAsync(
            JsonSerializer.Serialize(
                new
                {
                    schemaVersion = SchemaVersion,
                    operation = "preview",
                    title,
                    fields,
                },
                JsonOptions),
            "preview",
            cancellationToken).ConfigureAwait(false);
        return ParsePreview(response);
    }

    public async Task<AdrFormCreation> CreateAsync(
        AdrCreationProposal proposal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        if (!AdrCreationProposalFactory.MatchesConfigurationAndFingerprint(configuration, proposal))
        {
            throw new AgentCliException(
                "The authorized ADR proposal fingerprint, target, or CLI invocation changed; creation was rejected.");
        }

        var currentPreview = await PreviewAsync(
            proposal.Title,
            proposal.Fields,
            cancellationToken).ConfigureAwait(false);
        if (!string.Equals(currentPreview.Markdown, proposal.Preview.Markdown, StringComparison.Ordinal))
        {
            throw new AgentCliException(
                "The authorized ADR content changed after preview; creation was rejected.");
        }

        var response = await InvokeAsync(
            JsonSerializer.Serialize(
                new
                {
                    schemaVersion = SchemaVersion,
                    operation = "create",
                    title = proposal.Title,
                    fields = proposal.Fields,
                },
                JsonOptions),
            "create",
            cancellationToken).ConfigureAwait(false);
        var creation = ParseCreation(response);

        var fullPath = Path.GetFullPath(creation.Path, configuration.RepositoryRoot);
        string writtenContent;
        try
        {
            writtenContent = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new AgentCliException(
                "The created ADR could not be verified against the authorized preview.",
                exception);
        }

        if (!string.Equals(writtenContent, proposal.Preview.Markdown, StringComparison.Ordinal))
        {
            throw new AgentCliException(
                "The created ADR content did not match the authorized preview.");
        }

        return creation;
    }

    private async Task<string> InvokeAsync(
        string input,
        string operation,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(proposalConfiguration.CliCommand)
        {
            WorkingDirectory = configuration.RepositoryRoot,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false, true),
            StandardOutputEncoding = new UTF8Encoding(false, true),
            StandardErrorEncoding = new UTF8Encoding(false, true),
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in proposalConfiguration.CliArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        startInfo.ArgumentList.Add("new");
        startInfo.ArgumentList.Add(configuration.AdrDirectory);
        startInfo.ArgumentList.Add("--form");
        ProcessEnvironmentPolicy.Apply(startInfo);

        using var process = new Process { StartInfo = startInfo };
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(proposalConfiguration.CliTimeout);
        try
        {
            if (!process.Start())
            {
                throw new AgentCliException("The configured ADR Guard CLI process did not start.");
            }

            var outputTask = ReadBoundedAsync(
                process.StandardOutput,
                MaximumOutputCharacters,
                timeoutSource.Token);
            var errorTask = ReadBoundedAsync(
                process.StandardError,
                MaximumErrorCharacters,
                timeoutSource.Token);

            await process.StandardInput.WriteAsync(input.AsMemory(), timeoutSource.Token).ConfigureAwait(false);
            await process.StandardInput.WriteLineAsync().ConfigureAwait(false);
            process.StandardInput.Close();

            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
            var output = await outputTask.ConfigureAwait(false);
            _ = await errorTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                throw new AgentCliException(
                    $"ADR proposal {operation} was rejected by 'adr-guard new --form' with exit code {process.ExitCode}. No file was written.");
            }

            return output;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryTerminate(process);
            throw;
        }
        catch (OperationCanceledException exception)
        {
            TryTerminate(process);
            throw new AgentCliException(
                $"ADR proposal {operation} exceeded the configured CLI timeout.",
                exception);
        }
        catch (AgentCliException)
        {
            TryTerminate(process);
            throw;
        }
        catch (Exception exception) when (exception is InvalidDataException
            or IOException
            or InvalidOperationException
            or System.ComponentModel.Win32Exception)
        {
            TryTerminate(process);
            throw new AgentCliException(
                $"ADR proposal {operation} could not be completed by the configured CLI.",
                exception);
        }
    }

    private static AdrFormDescription ParseDescription(string response)
    {
        using var document = ParseResponse(response);
        var root = document.RootElement;
        RequireExactProperties(root, "schemaVersion", "operation", "template", "written");
        RequireEnvelope(root, "describe");
        if (root.GetProperty("written").ValueKind != JsonValueKind.False)
        {
            throw InvalidResponse("describe");
        }

        return ParseTemplate(root.GetProperty("template"), "describe");
    }

    private AdrFormPreview ParsePreview(string response)
    {
        using var document = ParseResponse(response);
        var root = document.RootElement;
        RequireExactProperties(
            root,
            "schemaVersion",
            "operation",
            "template",
            "path",
            "markdown",
            "written");
        RequireEnvelope(root, "preview");
        if (root.GetProperty("written").ValueKind != JsonValueKind.False)
        {
            throw new AgentCliException(
                "The ADR Guard CLI reported a write during proposal preview; the result was rejected.");
        }

        _ = ParseTemplate(root.GetProperty("template"), "preview");

        var path = ReadRequiredString(root, "path", "preview");
        var markdown = ReadRequiredString(root, "markdown", "preview", allowLineBreaks: true);
        var fullPath = Path.GetFullPath(path, configuration.RepositoryRoot);
        var adrRoot = Path.GetFullPath(configuration.AdrDirectory, configuration.RepositoryRoot);
        var relativeToAdrRoot = Path.GetRelativePath(adrRoot, fullPath);
        if (relativeToAdrRoot == ".."
            || relativeToAdrRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || Path.IsPathRooted(relativeToAdrRoot))
        {
            throw new AgentCliException(
                "The ADR Guard CLI returned a proposal destination outside the configured ADR directory.");
        }

        var relativePath = Path.GetRelativePath(configuration.RepositoryRoot, fullPath)
            .Replace(Path.DirectorySeparatorChar, '/');
        return new AdrFormPreview(relativePath, markdown);
    }

    private AdrFormCreation ParseCreation(string response)
    {
        using var document = ParseResponse(response);
        var root = document.RootElement;
        RequireExactProperties(root, "schemaVersion", "operation", "template", "path", "written");
        RequireEnvelope(root, "create");
        if (root.GetProperty("written").ValueKind != JsonValueKind.True)
        {
            throw new AgentCliException(
                "The ADR Guard CLI did not confirm the authorized ADR creation.");
        }

        _ = ParseTemplate(root.GetProperty("template"), "create");
        var path = ReadRequiredString(root, "path", "create");
        var fullPath = Path.GetFullPath(path, configuration.RepositoryRoot);
        var adrRoot = Path.GetFullPath(configuration.AdrDirectory, configuration.RepositoryRoot);
        var relativeToAdrRoot = Path.GetRelativePath(adrRoot, fullPath);
        if (relativeToAdrRoot == ".."
            || relativeToAdrRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || Path.IsPathRooted(relativeToAdrRoot))
        {
            throw new AgentCliException(
                "The ADR Guard CLI returned a creation destination outside the configured ADR directory.");
        }

        return new AdrFormCreation(
            Path.GetRelativePath(configuration.RepositoryRoot, fullPath)
                .Replace(Path.DirectorySeparatorChar, '/'));
    }

    private static AdrFormDescription ParseTemplate(JsonElement template, string operation)
    {
        if (template.ValueKind != JsonValueKind.Object
            || !template.TryGetProperty("compatible", out var compatible)
            || compatible.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw InvalidResponse(operation);
        }

        if (compatible.ValueKind == JsonValueKind.False)
        {
            RequireExactProperties(template, "name", "compatible", "reason", "fields");
            _ = ReadRequiredString(template, "reason", operation);
            throw new AgentCliException(
                "The selected ADR template is not compatible with the structured proposal workflow.");
        }

        RequireExactProperties(template, "name", "compatible", "fields");
        var name = ReadRequiredString(template, "name", operation);
        if (template.GetProperty("fields").ValueKind != JsonValueKind.Array)
        {
            throw InvalidResponse(operation);
        }

        var fields = new List<AdrFormField>();
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in template.GetProperty("fields").EnumerateArray())
        {
            RequireExactProperties(item, "id", "required");
            var identifier = ReadRequiredString(item, "id", operation);
            if (!identifiers.Add(identifier)
                || item.GetProperty("required").ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw InvalidResponse(operation);
            }
            fields.Add(new AdrFormField(
                identifier,
                item.GetProperty("required").GetBoolean()));
        }

        if (fields.Count == 0)
        {
            throw InvalidResponse(operation);
        }
        return new AdrFormDescription(name, fields.AsReadOnly());
    }

    private static JsonDocument ParseResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            throw InvalidResponse("form");
        }

        try
        {
            var document = JsonDocument.Parse(
                response,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 8,
                });
            RejectDuplicateProperties(document.RootElement, depth: 0);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                document.Dispose();
                throw InvalidResponse("form");
            }
            return document;
        }
        catch (JsonException exception)
        {
            throw new AgentCliException(
                "The ADR Guard CLI returned malformed structured form output.",
                exception);
        }
    }

    private static void RequireEnvelope(JsonElement root, string operation)
    {
        if (ReadRequiredString(root, "schemaVersion", operation) != SchemaVersion
            || ReadRequiredString(root, "operation", operation) != operation)
        {
            throw InvalidResponse(operation);
        }
    }

    private static string ReadRequiredString(
        JsonElement element,
        string propertyName,
        string operation,
        bool allowLineBreaks = false)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw InvalidResponse(operation);
        }

        var value = property.GetString()!;
        if (value.Any(character => char.IsControl(character)
            && !(allowLineBreaks && character is '\r' or '\n' or '\t')))
        {
            throw InvalidResponse(operation);
        }
        return value;
    }

    private static void RequireExactProperties(JsonElement element, params string[] properties)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.EnumerateObject().Select(property => property.Name)
                .ToHashSet(StringComparer.Ordinal)
                .SetEquals(properties))
        {
            throw InvalidResponse("form");
        }
    }

    private static void RejectDuplicateProperties(JsonElement element, int depth)
    {
        if (depth > 8)
        {
            throw new JsonException("The structured form output exceeded the maximum depth.");
        }
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new JsonException("The structured form output contained duplicate properties.");
                }
                RejectDuplicateProperties(property.Value, depth + 1);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                RejectDuplicateProperties(item, depth + 1);
            }
        }
    }

    private static async Task<string> ReadBoundedAsync(
        StreamReader reader,
        int maximumCharacters,
        CancellationToken cancellationToken)
    {
        var buffer = new char[4_096];
        var output = new StringBuilder();
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                return output.ToString();
            }
            if (output.Length + count > maximumCharacters)
            {
                throw new InvalidDataException("The configured CLI exceeded its bounded output limit.");
            }
            output.Append(buffer, 0, count);
        }
    }

    private static void TryTerminate(Process process)
    {
        try
        {
            if (process.StartTime != default && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or System.ComponentModel.Win32Exception
            or NotSupportedException)
        {
            // Best effort after a failed or canceled bounded process operation.
        }
    }

    private static AgentCliException InvalidResponse(string operation) =>
        new($"The ADR Guard CLI returned an unsupported schema 1.0 {operation} response.");
}
