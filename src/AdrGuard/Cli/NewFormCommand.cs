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
            var requestError = ValidateRequest(request);
            if (requestError is not null) return UsageError(requestError, error);

            if (templateFilePath is not null)
            {
                return WriteCustomTemplateResponse(request.Operation!, output, error);
            }

            var resolvedTemplate = templateName ?? AdrBuiltInTemplates.Minimal;
            var fieldDefinitions = AdrFormTemplateCatalog.Describe(resolvedTemplate);
            if (request.Operation == "describe")
            {
                WriteDescription(output, request.Operation!, resolvedTemplate, fieldDefinitions);
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
                request.Operation!,
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

    private static string? ValidateRequest(NewFormRequest request)
    {
        if (request.RequestedSchemaVersion != SchemaVersion)
            return $"Unsupported visual ADR form schema version '{request.RequestedSchemaVersion ?? "<missing>"}'.";
        if (request.Operation is not ("describe" or "preview" or "create"))
            return "Visual ADR form operation must be describe, preview, or create.";
        if (request.Operation == "describe" && (request.Title is not null || request.Fields is not null))
            return "Visual ADR form describe requests cannot contain title or fields.";
        if (request.Operation != "describe" && (request.Title is null || request.Fields is null))
            return "Visual ADR form preview and create requests require title and fields.";
        return null;
    }

    private static int WriteCustomTemplateResponse(string operation, TextWriter output, TextWriter error)
    {
        if (operation != "describe")
        {
            return UsageError(
                "Custom templates are not compatible with the visual form. Use the traditional 'new' flow.",
                error);
        }

        WriteResponse(output, new NewFormResponse(
            SchemaVersion,
            operation,
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

    private static void WriteDescription(
        TextWriter output,
        string operation,
        string template,
        IReadOnlyList<AdrFormFieldDefinition> fields) =>
        WriteResponse(output, new NewFormResponse(
            SchemaVersion,
            operation,
            new NewFormTemplateResponse(
                template,
                Compatible: true,
                Reason: null,
                fields.Select(field => new NewFormFieldResponse(field.Id, field.Required)).ToArray()),
            Path: null,
            Markdown: null,
            Written: false));

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
        var state = new FormArgumentState();

        for (var index = 1; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument == "--form")
            {
                if (!state.TrySetForm()) return SetOutputs(state, out directoryPath, out templateName,
                    out templateFilePath, out cultureName, out filenamePolicy, false);
                continue;
            }

            if (argument is "--template" or "--template-file" or "--culture" or "--filename-policy")
            {
                if (index + 1 >= args.Count) return SetOutputs(state, out directoryPath, out templateName,
                    out templateFilePath, out cultureName, out filenamePolicy, false);
                var value = args[++index];
                if (!state.TrySetValue(argument, value)) return SetOutputs(state, out directoryPath,
                    out templateName, out templateFilePath, out cultureName, out filenamePolicy, false);
                continue;
            }

            if (!state.TrySetDirectory(argument)) return SetOutputs(state, out directoryPath, out templateName,
                out templateFilePath, out cultureName, out filenamePolicy, false);
        }

        var valid = state.FormAssigned
            && !(state.TemplateName is not null && state.TemplateFilePath is not null)
            && (state.TemplateName is null
                || AdrBuiltInTemplates.Names.Contains(state.TemplateName, StringComparer.Ordinal));
        return SetOutputs(state, out directoryPath, out templateName, out templateFilePath,
            out cultureName, out filenamePolicy, valid);
    }

    private static bool SetOutputs(
        FormArgumentState state,
        out string directoryPath,
        out string? templateName,
        out string? templateFilePath,
        out string cultureName,
        out string? filenamePolicy,
        bool result)
    {
        directoryPath = state.DirectoryPath;
        templateName = state.TemplateName;
        templateFilePath = state.TemplateFilePath;
        cultureName = state.CultureName;
        filenamePolicy = state.FilenamePolicy;
        return result;
    }

    private sealed class FormArgumentState
    {
        private readonly HashSet<string> assigned = new(StringComparer.Ordinal);
        internal string DirectoryPath { get; private set; } = ".";
        internal string? TemplateName { get; private set; }
        internal string? TemplateFilePath { get; private set; }
        internal string CultureName { get; private set; } = "en-US";
        internal string? FilenamePolicy { get; private set; }
        internal bool FormAssigned => assigned.Contains("--form");

        internal bool TrySetForm() => assigned.Add("--form");

        internal bool TrySetValue(string argument, string value)
        {
            if (!assigned.Add(argument) || string.IsNullOrWhiteSpace(value) || value.StartsWith('-'))
                return false;
            switch (argument)
            {
                case "--template": TemplateName = value; break;
                case "--template-file": TemplateFilePath = value; break;
                case "--culture": CultureName = value; break;
                case "--filename-policy": FilenamePolicy = value; break;
            }
            return true;
        }

        internal bool TrySetDirectory(string argument)
        {
            if (argument.StartsWith('-') || !assigned.Add("directory")) return false;
            DirectoryPath = argument;
            return true;
        }
    }

    private static int UsageError(string message, TextWriter error)
    {
        error.WriteLine(message);
        error.WriteLine("Run 'adr-guard new --help' for usage.");
        return ExitCodes.UsageError;
    }

    private sealed record NewFormRequest(
        [property: JsonPropertyName("schemaVersion")] string? RequestedSchemaVersion,
        string? Operation,
        string? Title,
        Dictionary<string, string?>? Fields);

    private sealed record NewFormResponse(
        [property: JsonPropertyName("schemaVersion")] string ResponseSchemaVersion,
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
