using System.Text.Json;
using System.Text.Json.Serialization;
using AdrGuard.Generation;
using AdrGuard.Validation;

namespace AdrGuard.Cli;

internal static class NewFormCommand
{
    internal const string SchemaVersion = "1.0";
    internal const int MaximumInputCharacters = 128 * 1024;
    internal const int MaximumFieldCharacters = 32 * 1024;

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
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (!TryParse(
                args,
                out var directoryPath,
                out var templateName,
                out var templateFilePath,
                out var cultureName,
                out var filenamePolicyName))
        {
            return UsageError("Invalid arguments for the visual ADR form contract.", error);
        }

        if (!string.Equals(cultureName, "en-US", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(cultureName, "pt-BR", StringComparison.OrdinalIgnoreCase))
        {
            return UsageError(
                $"Unsupported template culture '{cultureName}'. Use en-US or pt-BR.",
                error);
        }

        cultureName = string.Equals(cultureName, "pt-BR", StringComparison.OrdinalIgnoreCase)
            ? "pt-BR"
            : "en-US";

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = ReadRequest(input, cancellationToken);
            if (request.SchemaVersion != SchemaVersion)
            {
                return UsageError(
                    $"Unsupported visual ADR form schema version '{request.SchemaVersion ?? "<missing>"}'.",
                    error);
            }

            if (request.Operation is not ("describe" or "preview" or "create"))
            {
                return UsageError("Visual ADR form operation must be describe, preview, or create.", error);
            }

            if (request.Operation == "describe" && (request.Title is not null || request.Fields is not null))
            {
                return UsageError("Visual ADR form describe requests cannot contain title or fields.", error);
            }

            if (request.Operation != "describe" && (request.Title is null || request.Fields is null))
            {
                return UsageError("Visual ADR form preview and create requests require title and fields.", error);
            }

            if (templateFilePath is not null)
            {
                if (request.Operation != "describe")
                {
                    return UsageError(
                        "Custom templates are not compatible with the visual form. Use the traditional 'new' flow.",
                        error);
                }

                WriteResponse(output, new NewFormResponse(
                    SchemaVersion,
                    request.Operation,
                    new NewFormTemplateResponse(
                        "custom",
                        Compatible: false,
                        "custom-template-field-mapping-unavailable",
                        []),
                    Path: null,
                    Markdown: null,
                    Written: false));
                return ExitCodes.Success;
            }

            var resolvedTemplate = templateName ?? AdrBuiltInTemplates.Minimal;
            var fieldDefinitions = AdrFormTemplateCatalog.Describe(resolvedTemplate);
            if (request.Operation == "describe")
            {
                WriteResponse(output, new NewFormResponse(
                    SchemaVersion,
                    request.Operation,
                    new NewFormTemplateResponse(
                        resolvedTemplate,
                        Compatible: true,
                        Reason: null,
                        fieldDefinitions
                            .Select(field => new NewFormFieldResponse(field.Id, field.Required))
                            .ToArray()),
                    Path: null,
                    Markdown: null,
                    Written: false));
                return ExitCodes.Success;
            }

            if (!Directory.Exists(directoryPath))
            {
                error.WriteLine($"ADR directory does not exist: '{directoryPath}'.");
                return ExitCodes.OperationalError;
            }

            var values = ValidateFields(request, fieldDefinitions);
            var title = request.Title?.Trim() ?? string.Empty;
            if (title.Length is 0 or > 512
                || title.IndexOfAny(['\r', '\n']) >= 0
                || string.IsNullOrWhiteSpace(AdrSlug.Create(title)))
            {
                return UsageError(
                    "ADR title must be one line and contain at least one ASCII letter or digit.",
                    error);
            }

            var definition = AdrFormTemplateCatalog.Get(
                resolvedTemplate,
                cultureName,
                values);
            var result = new AdrNewService()
                .CreateAsync(
                    directoryPath,
                    title,
                    definition.Template,
                    dryRun: request.Operation == "preview",
                    cancellationToken,
                    AdrFilenamePolicy.Parse(filenamePolicyName),
                    values)
                .GetAwaiter()
                .GetResult();

            if (!result.ValidationResult.IsValid)
            {
                ValidationOutput.WriteIssues(result.ValidationResult, error);
                return ExitCodes.ValidationFailed;
            }

            WriteResponse(output, new NewFormResponse(
                SchemaVersion,
                request.Operation,
                new NewFormTemplateResponse(
                    definition.Name,
                    Compatible: true,
                    Reason: null,
                    definition.Fields
                        .Select(field => new NewFormFieldResponse(field.Id, field.Required))
                        .ToArray()),
                result.FilePath,
                request.Operation == "preview" ? result.Content : null,
                result.Written));
            return ExitCodes.Success;
        }
        catch (JsonException exception)
        {
            return UsageError($"Visual ADR form input is invalid JSON: {exception.Message}", error);
        }
        catch (ArgumentException exception)
        {
            return UsageError(exception.Message, error);
        }
        catch (Exception exception) when (exception is InvalidDataException
            or IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or OperationCanceledException)
        {
            error.WriteLine($"Unable to create ADR from the visual form: {exception.Message}");
            return ExitCodes.OperationalError;
        }
    }

    private static NewFormRequest ReadRequest(
        TextReader input,
        CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        var builder = new System.Text.StringBuilder();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = input.Read(
                buffer,
                0,
                Math.Min(buffer.Length, MaximumInputCharacters + 1 - builder.Length));
            if (count == 0) break;
            builder.Append(buffer, 0, count);
            if (builder.Length > MaximumInputCharacters)
            {
                throw new InvalidDataException(
                    $"Visual ADR form input exceeds the {MaximumInputCharacters}-character limit.");
            }
        }

        var json = builder.ToString();
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
        RejectDuplicateProperties(document.RootElement, "$", depth: 0);
        return JsonSerializer.Deserialize<NewFormRequest>(json, JsonOptions)
            ?? throw new JsonException("Visual ADR form input is empty.");
    }

    private static Dictionary<string, string> ValidateFields(
        NewFormRequest request,
        IReadOnlyList<AdrFormFieldDefinition> definitions)
    {
        if (request.Fields is null)
        {
            throw new ArgumentException("Visual ADR form fields are required.");
        }

        var expected = definitions.ToDictionary(field => field.Id, StringComparer.Ordinal);
        if (request.Fields.Keys.Any(key => !expected.ContainsKey(key)))
        {
            throw new ArgumentException("Visual ADR form input contains an unsupported field.");
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in definitions)
        {
            var value = request.Fields.GetValueOrDefault(field.Id) ?? string.Empty;
            if (value.Length > MaximumFieldCharacters || value.Contains('\0'))
            {
                throw new ArgumentException(
                    $"Visual ADR form field '{field.Id}' is invalid or exceeds the size limit.");
            }

            if (field.Required && string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    $"Visual ADR form field '{field.Id}' is required.");
            }

            values[field.Id] = value;
        }

        return values;
    }

    private static void RejectDuplicateProperties(JsonElement element, string path, int depth)
    {
        if (depth > 8) throw new JsonException("Visual ADR form input exceeds the maximum depth.");
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new JsonException($"Duplicate property '{property.Name}' at '{path}'.");
                }
                RejectDuplicateProperties(property.Value, $"{path}.{property.Name}", depth + 1);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                RejectDuplicateProperties(item, path, depth + 1);
            }
        }
    }

    private static void WriteResponse(TextWriter output, NewFormResponse response) =>
        output.WriteLine(JsonSerializer.Serialize(response, JsonOptions));

    private static bool TryParse(
        IReadOnlyList<string> args,
        out string directoryPath,
        out string? templateName,
        out string? templateFilePath,
        out string cultureName,
        out string? filenamePolicy)
    {
        directoryPath = ".";
        templateName = null;
        templateFilePath = null;
        cultureName = "en-US";
        filenamePolicy = null;
        var directoryAssigned = false;
        var formAssigned = false;
        var cultureAssigned = false;

        for (var index = 1; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument == "--form")
            {
                if (formAssigned) return false;
                formAssigned = true;
                continue;
            }

            if (argument is "--template" or "--template-file" or "--culture" or "--filename-policy")
            {
                if (index + 1 >= args.Count) return false;
                var value = args[++index];
                if (string.IsNullOrWhiteSpace(value) || value.StartsWith('-')) return false;
                switch (argument)
                {
                    case "--template":
                        if (templateName is not null) return false;
                        templateName = value;
                        break;
                    case "--template-file":
                        if (templateFilePath is not null) return false;
                        templateFilePath = value;
                        break;
                    case "--culture":
                        if (cultureAssigned) return false;
                        cultureName = value;
                        cultureAssigned = true;
                        break;
                    case "--filename-policy":
                        if (filenamePolicy is not null) return false;
                        filenamePolicy = value;
                        break;
                }
                continue;
            }

            if (argument.StartsWith('-') || directoryAssigned) return false;
            directoryPath = argument;
            directoryAssigned = true;
        }

        return formAssigned
            && !(templateName is not null && templateFilePath is not null)
            && (templateName is null || AdrBuiltInTemplates.Names.Contains(templateName, StringComparer.Ordinal));
    }

    private static int UsageError(string message, TextWriter error)
    {
        error.WriteLine(message);
        error.WriteLine("Run 'adr-guard new --help' for usage.");
        return ExitCodes.UsageError;
    }

    private sealed record NewFormRequest(
        string? SchemaVersion,
        string? Operation,
        string? Title,
        Dictionary<string, string?>? Fields);

    private sealed record NewFormResponse(
        string SchemaVersion,
        string Operation,
        NewFormTemplateResponse Template,
        string? Path,
        string? Markdown,
        bool Written);

    private sealed record NewFormTemplateResponse(
        string Name,
        bool Compatible,
        string? Reason,
        IReadOnlyList<NewFormFieldResponse> Fields);

    private sealed record NewFormFieldResponse(string Id, bool Required);
}
