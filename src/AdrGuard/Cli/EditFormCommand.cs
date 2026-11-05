using System.Text.Json;
using System.Text.Json.Serialization;
using AdrGuard.Editing;
using AdrGuard.Validation;

namespace AdrGuard.Cli;

internal static class EditFormCommand
{
    internal const string SchemaVersion = "1.0";
    internal const int MaximumInputCharacters = 384 * 1024;

    internal const string HelpText = """
        Usage:
          adr-guard edit <adr-file> --form [--adr-directory <directory>] [--adr-format canonical|madr-4] [--lifecycle-statuses <mapping>] [--conventional-supersession] [--filename-policy <policy>] [--placeholder-policy off|warn|error] [--validate-metadata] [--validation-profile <profile>]

        Load or update one existing ADR through a bounded schema 1.0 JSON contract.
        Supported operations are load and save. Save requires the fingerprint returned by load,
        preserves the file path and untouched Markdown, validates the result, and fails on external changes.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    internal static int Run(
        IReadOnlyList<string> args,
        TextReader input,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (args.Count == 2 && args[1] is "--help" or "-h")
        {
            output.WriteLine(HelpText);
            return ExitCodes.Success;
        }
        if (!TryParse(args, out var parsed)) return UsageError("Invalid arguments for 'edit'.", error);

        try
        {
            var parsedRequest = ReadRequest(input, cancellationToken);
            var request = parsedRequest.Request;
            var requestError = ValidateRequest(parsedRequest);
            if (requestError is not null) return UsageError(requestError, error);

            var options = AdrValidationOptionsFactory.Create(
                parsed.Format,
                Directory.GetCurrentDirectory(),
                parsed.LifecycleStatuses,
                parsed.ConventionalSupersession,
                parsed.FilenamePolicy,
                parsed.PlaceholderPolicy,
                parsed.ValidateMetadata,
                parsed.ValidationProfile);
            var document = request.Operation == "load"
                ? AdrEditService.Load(parsed.FilePath, parsed.AdrDirectory, options, cancellationToken)
                : AdrEditService.Save(parsed.FilePath, request.Fingerprint!, request.Title!, request.Fields!, parsed.AdrDirectory, options, cancellationToken);
            WriteDocumentResponse(output, request.Operation!, parsed.Format, document);
            return ExitCodes.Success;
        }
        catch (AdrEditValidationException exception)
        {
            ValidationOutput.WriteIssues(exception.Result, error);
            return ExitCodes.ValidationFailed;
        }
        catch (JsonException exception)
        {
            return UsageError($"ADR edit input is invalid JSON: {exception.Message}", error);
        }
        catch (ArgumentException exception)
        {
            return UsageError(exception.Message, error);
        }
        catch (Exception exception) when (exception is InvalidDataException
            or IOException
            or UnauthorizedAccessException
            or OperationCanceledException)
        {
            error.WriteLine($"Unable to edit ADR: {exception.Message}");
            return ExitCodes.OperationalError;
        }
    }

    private static string? ValidateRequest(ParsedEditFormRequest parsedRequest)
    {
        var request = parsedRequest.Request;
        if (request.RequestedSchemaVersion != SchemaVersion || request.Operation is not ("load" or "save"))
            return "ADR edit schema version must be 1.0 and operation must be load or save.";
        var expectedProperties = request.Operation == "load" ? LoadProperties : SaveProperties;
        if (!parsedRequest.Properties.SetEquals(expectedProperties))
            return $"ADR edit {request.Operation} request has an invalid property set.";
        if (request.Operation == "load"
            && (request.Fingerprint is not null || request.Title is not null || request.Fields is not null))
            return "ADR edit load requests cannot contain fingerprint, title, or fields.";
        if (request.Operation == "save"
            && (request.Fingerprint is null || request.Title is null || request.Fields is null))
            return "ADR edit save requests require fingerprint, title, and fields.";
        if (request.Fingerprint is not null
            && (request.Fingerprint.Length != 64
                || request.Fingerprint.Any(character => !char.IsAsciiHexDigit(character))))
            return "ADR edit fingerprint must be a 64-character hexadecimal SHA-256 value.";
        return null;
    }

    private static void WriteDocumentResponse(
        TextWriter output,
        string operation,
        AdrFormat format,
        AdrEditDocument document)
    {
        if (operation == "save")
        {
            WriteResponse(output, new EditSaveResponse(
                SchemaVersion,
                operation,
                document.Fingerprint,
                Written: document.Written));
            return;
        }

        WriteResponse(output, new EditLoadResponse(
            SchemaVersion,
            operation,
            format == AdrFormat.Madr4 ? "madr-4" : "canonical",
            document.FilePath,
            document.Fingerprint,
            document.Title,
            document.Fields.Select(field => new EditFieldResponse(
                field.Id, field.Label, field.Kind, field.Required, field.Value, field.Options)).ToArray(),
            Written: document.Written));
    }

    private static readonly HashSet<string> LoadProperties = new(StringComparer.Ordinal)
    {
        "schemaVersion", "operation",
    };

    private static readonly HashSet<string> SaveProperties = new(StringComparer.Ordinal)
    {
        "schemaVersion", "operation", "fingerprint", "title", "fields",
    };

    private static ParsedEditFormRequest ReadRequest(TextReader input, CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        var builder = new System.Text.StringBuilder();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = input.Read(buffer, 0, Math.Min(buffer.Length, MaximumInputCharacters + 1 - builder.Length));
            if (count == 0) break;
            builder.Append(buffer, 0, count);
            if (builder.Length > MaximumInputCharacters)
                throw new InvalidDataException($"ADR edit input exceeds the {MaximumInputCharacters}-character limit.");
        }
        var json = builder.ToString();
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new JsonException("ADR edit input must be a JSON object.");
        RejectDuplicateProperties(document.RootElement, depth: 0);
        var request = JsonSerializer.Deserialize<EditFormRequest>(json, JsonOptions)
            ?? throw new JsonException("ADR edit input is empty.");
        return new ParsedEditFormRequest(
            request,
            document.RootElement.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal));
    }

    private static void RejectDuplicateProperties(JsonElement element, int depth)
    {
        if (depth > 8) throw new JsonException("ADR edit input exceeds the maximum depth.");
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException($"Duplicate property '{property.Name}'.");
                RejectDuplicateProperties(property.Value, depth + 1);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) RejectDuplicateProperties(item, depth + 1);
        }
    }

    private static bool TryParse(IReadOnlyList<string> args, out EditArguments result)
    {
        var state = new EditArgumentState();
        for (var index = 1; index < args.Count; index++)
        {
            if (!TryApplyArgument(args, ref index, state))
            {
                result = EditArguments.Empty;
                return false;
            }
        }
        result = state.ToArguments();
        return state.Form && !string.IsNullOrWhiteSpace(state.FilePath);
    }

    private static bool TryApplyArgument(
        IReadOnlyList<string> args,
        ref int index,
        EditArgumentState state)
    {
        var argument = args[index];
        if (argument is "--form" or "--conventional-supersession" or "--validate-metadata")
            return state.TryAddFlag(argument);
        if (argument is not ("--adr-directory" or "--adr-format" or "--lifecycle-statuses"
            or "--filename-policy" or "--placeholder-policy" or "--validation-profile"))
            return state.TrySetFilePath(argument);
        if (index + 1 >= args.Count) return false;
        return state.TryAddValue(argument, args[++index]);
    }

    private static int UsageError(string message, TextWriter error)
    {
        error.WriteLine(message);
        error.WriteLine("Run 'adr-guard edit --help' for usage.");
        return ExitCodes.UsageError;
    }

    private static void WriteResponse<TResponse>(TextWriter output, TResponse response) =>
        output.WriteLine(JsonSerializer.Serialize(response, JsonOptions));

    private sealed record EditFormRequest(
        [property: JsonPropertyName("schemaVersion")] string? RequestedSchemaVersion,
        string? Operation,
        string? Fingerprint,
        string? Title,
        Dictionary<string, string>? Fields);
    private sealed record ParsedEditFormRequest(EditFormRequest Request, HashSet<string> Properties);
    private sealed record EditLoadResponse(
        [property: JsonPropertyName("schemaVersion")] string ResponseSchemaVersion,
        string Operation,
        string Format,
        string Path,
        string Fingerprint,
        string Title,
        IReadOnlyList<EditFieldResponse> Fields,
        bool Written);
    private sealed record EditSaveResponse(
        [property: JsonPropertyName("schemaVersion")] string ResponseSchemaVersion,
        string Operation,
        string Fingerprint,
        bool Written);
    private sealed record EditFieldResponse(string Id, string Label, string Kind, bool Required, string Value, IReadOnlyList<string>? Options);
    private sealed record EditArguments(string FilePath, string? AdrDirectory, AdrFormat Format, string? LifecycleStatuses, bool ConventionalSupersession, string? FilenamePolicy, string? PlaceholderPolicy, bool ValidateMetadata, string? ValidationProfile)
    {
        internal static EditArguments Empty { get; } = new(string.Empty, null, AdrFormat.Canonical, null, false, null, null, false, null);
    }

    private sealed class EditArgumentState
    {
        private readonly HashSet<string> assigned = new(StringComparer.Ordinal);

        internal string? FilePath { get; private set; }
        internal string? AdrDirectory { get; private set; }
        internal AdrFormat Format { get; private set; } = AdrFormat.Canonical;
        internal string? Lifecycle { get; private set; }
        internal string? FilenamePolicy { get; private set; }
        internal string? PlaceholderPolicy { get; private set; }
        internal string? ValidationProfile { get; private set; }
        internal bool Form { get; private set; }
        internal bool Conventional { get; private set; }
        internal bool ValidateMetadata { get; private set; }

        internal bool TryAddFlag(string argument)
        {
            if (!assigned.Add(argument)) return false;
            if (argument == "--form") Form = true;
            else if (argument == "--conventional-supersession") Conventional = true;
            else ValidateMetadata = true;
            return true;
        }

        internal bool TryAddValue(string argument, string value)
        {
            if (!assigned.Add(argument) || string.IsNullOrWhiteSpace(value) || value.StartsWith('-'))
                return false;
            switch (argument)
            {
                case "--adr-directory": AdrDirectory = value; break;
                case "--adr-format":
                    Format = value switch
                    {
                        "canonical" => AdrFormat.Canonical,
                        "madr-4" => AdrFormat.Madr4,
                        _ => (AdrFormat)(-1),
                    };
                    return Enum.IsDefined(Format);
                case "--lifecycle-statuses": Lifecycle = value; break;
                case "--filename-policy": FilenamePolicy = value; break;
                case "--placeholder-policy": PlaceholderPolicy = value; break;
                case "--validation-profile": ValidationProfile = value; break;
            }
            return true;
        }

        internal bool TrySetFilePath(string argument)
        {
            if (argument.StartsWith('-') || FilePath is not null) return false;
            FilePath = argument;
            return true;
        }

        internal EditArguments ToArguments() => new(
            FilePath ?? string.Empty,
            AdrDirectory,
            Format,
            Lifecycle,
            Conventional,
            FilenamePolicy,
            PlaceholderPolicy,
            ValidateMetadata,
            ValidationProfile);
    }
}
