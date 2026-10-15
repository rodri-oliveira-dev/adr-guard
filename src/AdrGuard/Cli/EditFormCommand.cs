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
            if (request.SchemaVersion != SchemaVersion || request.Operation is not ("load" or "save"))
                return UsageError("ADR edit schema version must be 1.0 and operation must be load or save.", error);
            var expectedProperties = request.Operation == "load"
                ? LoadProperties
                : SaveProperties;
            if (!parsedRequest.Properties.SetEquals(expectedProperties))
                return UsageError($"ADR edit {request.Operation} request has an invalid property set.", error);
            if (request.Operation == "load" && (request.Fingerprint is not null || request.Title is not null || request.Fields is not null))
                return UsageError("ADR edit load requests cannot contain fingerprint, title, or fields.", error);
            if (request.Operation == "save" && (request.Fingerprint is null || request.Title is null || request.Fields is null))
                return UsageError("ADR edit save requests require fingerprint, title, and fields.", error);
            if (request.Fingerprint is not null
                && (request.Fingerprint.Length != 64 || request.Fingerprint.Any(character => !char.IsAsciiHexDigit(character))))
                return UsageError("ADR edit fingerprint must be a 64-character hexadecimal SHA-256 value.", error);

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
            if (request.Operation == "save")
            {
                WriteResponse(output, new EditSaveResponse(
                    SchemaVersion,
                    request.Operation,
                    document.Fingerprint,
                    Written: document.Written));
            }
            else
            {
                WriteResponse(output, new EditLoadResponse(
                    SchemaVersion,
                    request.Operation,
                    parsed.Format == AdrFormat.Madr4 ? "madr-4" : "canonical",
                    document.FilePath,
                    document.Fingerprint,
                    document.Title,
                    document.Fields.Select(field => new EditFieldResponse(
                        field.Id, field.Label, field.Kind, field.Required, field.Value, field.Options)).ToArray(),
                    Written: document.Written));
            }
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
        string? filePath = null;
        string? adrDirectory = null;
        var format = AdrFormat.Canonical;
        string? lifecycle = null;
        string? filenamePolicy = null;
        string? placeholderPolicy = null;
        string? validationProfile = null;
        var form = false;
        var conventional = false;
        var validateMetadata = false;
        var assigned = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument is "--form" or "--conventional-supersession" or "--validate-metadata")
            {
                if (!assigned.Add(argument)) { result = EditArguments.Empty; return false; }
                if (argument == "--form") form = true;
                else if (argument == "--conventional-supersession") conventional = true;
                else validateMetadata = true;
                continue;
            }
            if (argument is "--adr-directory" or "--adr-format" or "--lifecycle-statuses" or "--filename-policy" or "--placeholder-policy" or "--validation-profile")
            {
                if (!assigned.Add(argument) || index + 1 >= args.Count) { result = EditArguments.Empty; return false; }
                var value = args[++index];
                if (string.IsNullOrWhiteSpace(value) || value.StartsWith('-')) { result = EditArguments.Empty; return false; }
                switch (argument)
                {
                    case "--adr-directory": adrDirectory = value; break;
                    case "--adr-format":
                        format = value switch { "canonical" => AdrFormat.Canonical, "madr-4" => AdrFormat.Madr4, _ => (AdrFormat)(-1) };
                        if (!Enum.IsDefined(format)) { result = EditArguments.Empty; return false; }
                        break;
                    case "--lifecycle-statuses": lifecycle = value; break;
                    case "--filename-policy": filenamePolicy = value; break;
                    case "--placeholder-policy": placeholderPolicy = value; break;
                    case "--validation-profile": validationProfile = value; break;
                }
                continue;
            }
            if (argument.StartsWith('-') || filePath is not null) { result = EditArguments.Empty; return false; }
            filePath = argument;
        }
        result = new EditArguments(filePath ?? string.Empty, adrDirectory, format, lifecycle, conventional, filenamePolicy, placeholderPolicy, validateMetadata, validationProfile);
        return form && !string.IsNullOrWhiteSpace(filePath);
    }

    private static int UsageError(string message, TextWriter error)
    {
        error.WriteLine(message);
        error.WriteLine("Run 'adr-guard edit --help' for usage.");
        return ExitCodes.UsageError;
    }

    private static void WriteResponse<TResponse>(TextWriter output, TResponse response) =>
        output.WriteLine(JsonSerializer.Serialize(response, JsonOptions));

    private sealed record EditFormRequest(string? SchemaVersion, string? Operation, string? Fingerprint, string? Title, Dictionary<string, string>? Fields);
    private sealed record ParsedEditFormRequest(EditFormRequest Request, HashSet<string> Properties);
    private sealed record EditLoadResponse(string SchemaVersion, string Operation, string Format, string Path, string Fingerprint, string Title, IReadOnlyList<EditFieldResponse> Fields, bool Written);
    private sealed record EditSaveResponse(string SchemaVersion, string Operation, string Fingerprint, bool Written);
    private sealed record EditFieldResponse(string Id, string Label, string Kind, bool Required, string Value, IReadOnlyList<string>? Options);
    private sealed record EditArguments(string FilePath, string? AdrDirectory, AdrFormat Format, string? LifecycleStatuses, bool ConventionalSupersession, string? FilenamePolicy, string? PlaceholderPolicy, bool ValidateMetadata, string? ValidationProfile)
    {
        internal static EditArguments Empty { get; } = new(string.Empty, null, AdrFormat.Canonical, null, false, null, null, false, null);
    }
}
