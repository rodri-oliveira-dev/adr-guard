using System.Text;

namespace AdrGuard.Configuration;

internal static class AdrGuardConfigurationLoader
{
    internal const string FileName = ".adrguard.yml";
    private const int MaximumBytes = 64 * 1024;

    private static readonly HashSet<string> KnownKeys =
    [
        "schema-version",
        "adr-directory",
        "template",
        "template-file",
        "adr-format",
        "lifecycle-statuses",
        "conventional-supersession",
        "filename-policy",
        "placeholder-policy",
        "metadata-policy",
        "validation-profile",
    ];

    internal static AdrGuardConfiguration? Load(string invocationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationDirectory);

        var root = Path.GetFullPath(invocationDirectory);
        var path = Path.Combine(root, FileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new AdrGuardConfigurationException(
                    $"Configuration file must not be a symbolic link or reparse point: '{path}'.");
            }

            var bytes = File.ReadAllBytes(path);
            if (bytes.Length > MaximumBytes)
            {
                throw new AdrGuardConfigurationException(
                    $"Configuration file exceeds the {MaximumBytes}-byte limit: '{path}'.");
            }

            var utf8 = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true);
            var text = utf8.GetString(bytes);
            var values = Parse(path, text);
            return Create(path, root, values);
        }
        catch (AdrGuardConfigurationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or DecoderFallbackException
            or ArgumentException)
        {
            throw new AdrGuardConfigurationException(
                $"Unable to load configuration '{path}': {exception.Message}",
                exception);
        }
    }

    private static Dictionary<string, string> Parse(string path, string text)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        using var reader = new StringReader(text);
        var lineNumber = 0;

        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            if (char.IsWhiteSpace(line[0]) || trimmed is "---" or "...")
            {
                throw Invalid(path, lineNumber, "only top-level scalar properties are supported");
            }

            var separator = trimmed.IndexOf(':');
            if (separator <= 0)
            {
                throw Invalid(path, lineNumber, "expected 'property: value'");
            }

            var key = trimmed[..separator].Trim();
            if (!KnownKeys.Contains(key))
            {
                throw Invalid(path, lineNumber, $"unknown property '{key}'");
            }

            if (!values.TryAdd(key, ParseScalar(path, lineNumber, trimmed[(separator + 1)..])))
            {
                throw Invalid(path, lineNumber, $"duplicate property '{key}'");
            }
        }

        return values;
    }

    private static string ParseScalar(string path, int lineNumber, string rawValue)
    {
        var value = rawValue.Trim();
        if (value.Length == 0)
        {
            throw Invalid(path, lineNumber, "property value cannot be empty");
        }

        if (value[0] == '\'')
        {
            if (value.Length < 2 || value[^1] != '\'')
            {
                throw Invalid(path, lineNumber, "unterminated single-quoted scalar");
            }

            return value[1..^1].Replace("''", "'", StringComparison.Ordinal);
        }

        if (value[0] == '"')
        {
            if (value.Length < 2 || value[^1] != '"')
            {
                throw Invalid(path, lineNumber, "unterminated double-quoted scalar");
            }

            return UnescapeDoubleQuoted(path, lineNumber, value[1..^1]);
        }

        var comment = value.IndexOf(" #", StringComparison.Ordinal);
        if (comment >= 0)
        {
            value = value[..comment].TrimEnd();
        }

        if (value.IndexOfAny(['[', ']', '{', '}', '&', '*', '!', '|', '>']) >= 0)
        {
            throw Invalid(path, lineNumber, "collections, tags, anchors, aliases, and block scalars are not supported");
        }

        return value;
    }

    private static string UnescapeDoubleQuoted(string path, int lineNumber, string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '\\')
            {
                builder.Append(value[index]);
                continue;
            }

            if (++index >= value.Length)
            {
                throw Invalid(path, lineNumber, "invalid trailing escape");
            }

            builder.Append(value[index] switch
            {
                '\\' => '\\',
                '"' => '"',
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                _ => throw Invalid(path, lineNumber, $"unsupported escape '\\{value[index]}'"),
            });
        }

        return builder.ToString();
    }

    private static AdrGuardConfiguration Create(
        string path,
        string root,
        Dictionary<string, string> values)
    {
        if (!values.TryGetValue("schema-version", out var schemaVersion))
        {
            throw new AdrGuardConfigurationException(
                $"Configuration '{path}' is missing required property 'schema-version'.");
        }

        if (schemaVersion != "1")
        {
            throw new AdrGuardConfigurationException(
                $"Configuration '{path}' uses unsupported schema-version '{schemaVersion}'; expected '1'.");
        }

        values.TryGetValue("adr-directory", out var adrDirectory);
        values.TryGetValue("template", out var template);
        values.TryGetValue("template-file", out var templateFile);
        values.TryGetValue("adr-format", out var adrFormat);
        values.TryGetValue("lifecycle-statuses", out var lifecycleStatuses);
        values.TryGetValue("conventional-supersession", out var conventionalSupersessionText);
        values.TryGetValue("filename-policy", out var filenamePolicy);
        values.TryGetValue("placeholder-policy", out var placeholderPolicy);
        values.TryGetValue("metadata-policy", out var metadataPolicy);
        values.TryGetValue("validation-profile", out var validationProfile);
        var conventionalSupersession = conventionalSupersessionText switch
        {
            null or "false" => false,
            "true" => true,
            _ => throw new AdrGuardConfigurationException(
                $"Configuration '{path}' property 'conventional-supersession' must be 'true' or 'false'."),
        };

        if (template is not null && templateFile is not null)
        {
            throw new AdrGuardConfigurationException(
                $"Configuration '{path}' cannot define both 'template' and 'template-file'.");
        }

        if (template is not null && template is not ("minimal" or "extended"))
        {
            throw new AdrGuardConfigurationException(
                $"Configuration '{path}' property 'template' must be 'minimal' or 'extended'.");
        }

        if (adrFormat is not null && adrFormat is not ("canonical" or "madr-4"))
        {
            throw new AdrGuardConfigurationException(
                $"Configuration '{path}' property 'adr-format' must be 'canonical' or 'madr-4'.");
        }

        if (lifecycleStatuses is not null)
        {
            try
            {
                Validation.AdrLifecyclePolicy.Parse(lifecycleStatuses);
            }
            catch (ArgumentException exception)
            {
                throw new AdrGuardConfigurationException(
                    $"Configuration '{path}' property 'lifecycle-statuses' is invalid: {exception.Message}",
                    exception);
            }
        }

        if (filenamePolicy is not null)
        {
            try { Validation.AdrFilenamePolicy.Parse(filenamePolicy); }
            catch (ArgumentException exception)
            {
                throw new AdrGuardConfigurationException(
                    $"Configuration '{path}' property 'filename-policy' is invalid: {exception.Message}", exception);
            }
        }
        if (placeholderPolicy is not null && placeholderPolicy is not ("off" or "warn" or "error"))
            throw new AdrGuardConfigurationException($"Configuration '{path}' property 'placeholder-policy' must be off, warn, or error.");
        if (metadataPolicy is not null && metadataPolicy is not ("off" or "validate"))
            throw new AdrGuardConfigurationException($"Configuration '{path}' property 'metadata-policy' must be off or validate.");
        try { Validation.AdrValidationOptionsFactory.ParseProfile(validationProfile); }
        catch (ArgumentException exception)
        {
            throw new AdrGuardConfigurationException($"Configuration '{path}' property 'validation-profile' is invalid: {exception.Message}", exception);
        }

        try
        {
            var normalizedDirectory = RepositoryPath.NormalizeRelative(
                root,
                adrDirectory ?? ".");
            string? normalizedTemplateFile = null;
            if (templateFile is not null)
            {
                normalizedTemplateFile = RepositoryPath.NormalizeRelative(root, templateFile);
            }

            return new AdrGuardConfiguration(
                root,
                normalizedDirectory,
                template,
                normalizedTemplateFile,
                adrFormat,
                lifecycleStatuses,
                conventionalSupersession,
                filenamePolicy,
                placeholderPolicy,
                metadataPolicy == "validate",
                validationProfile);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException)
        {
            throw new AdrGuardConfigurationException(
                $"Configuration '{path}' contains an unsafe path: {exception.Message}",
                exception);
        }
    }

    private static AdrGuardConfigurationException Invalid(
        string path,
        int lineNumber,
        string reason) =>
        new($"Invalid configuration '{path}' at line {lineNumber}: {reason}.");
}
