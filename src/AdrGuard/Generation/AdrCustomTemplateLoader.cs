using AdrGuard.Parsing;
using AdrGuard.Validation;
using System.Text;
using System.Text.RegularExpressions;

namespace AdrGuard.Generation;

/// <summary>
/// Reads only a caller-selected local Markdown template. The template defines
/// section bodies, not output paths, scripts, status, or canonical structure.
/// </summary>
internal static class AdrCustomTemplateLoader
{
    internal const int MaximumTemplateBytes = 64 * 1024;

    internal static AdrTemplateDefinition Load(
        string templateFilePath,
        string cultureName,
        string? invocationDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);
        cancellationToken.ThrowIfCancellationRequested();

        var sourcePath = Path.GetFullPath(
            templateFilePath,
            Path.GetFullPath(invocationDirectory ?? Environment.CurrentDirectory));

        if (!string.Equals(
                Path.GetExtension(sourcePath),
                ".md",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"--template-file must reference a local Markdown (.md) file: '{sourcePath}'.",
                nameof(templateFilePath));
        }

        byte[] bytes;
        try
        {
            using var input = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                options: FileOptions.SequentialScan);

            if (input.Length > MaximumTemplateBytes)
            {
                throw new InvalidDataException(
                    $"Custom ADR template '{sourcePath}' exceeds the {MaximumTemplateBytes}-byte UTF-8 limit.");
            }

            using var buffer = new MemoryStream();
            var chunk = new byte[4096];

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var count = input.Read(
                    chunk,
                    0,
                    Math.Min(chunk.Length, MaximumTemplateBytes + 1 - (int)buffer.Length));

                if (count == 0)
                {
                    break;
                }

                buffer.Write(chunk, 0, count);

                if (buffer.Length > MaximumTemplateBytes)
                {
                    throw new InvalidDataException(
                        $"Custom ADR template '{sourcePath}' exceeds the {MaximumTemplateBytes}-byte UTF-8 limit.");
                }
            }

            bytes = buffer.ToArray();
        }
        catch (FileNotFoundException exception)
        {
            throw new IOException(
                $"Custom ADR template file not found: '{sourcePath}'. Pass an existing local Markdown file to --template-file.",
                exception);
        }
        catch (DirectoryNotFoundException exception)
        {
            throw new IOException(
                $"Custom ADR template directory not found for '{sourcePath}'. Resolve --template-file relative to the invocation directory.",
                exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new IOException(
                $"Custom ADR template '{sourcePath}' cannot be read (access denied).",
                exception);
        }

        cancellationToken.ThrowIfCancellationRequested();

        string source;
        try
        {
            source = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException(
                $"Custom ADR template '{sourcePath}' must contain valid UTF-8 text.",
                exception);
        }

        if (source.StartsWith('\uFEFF'))
        {
            source = source[1..];
        }

        if (source.Contains('\0'))
        {
            throw new InvalidDataException(
                $"Custom ADR template '{sourcePath}' contains an unsupported NUL character.");
        }

        if (source.Contains("${", StringComparison.Ordinal)
            || source.Contains("{%", StringComparison.Ordinal)
            || Regex.IsMatch(
                source,
                @"(?<!\{)\{[A-Za-z][A-Za-z0-9_-]*\}(?!\})",
                RegexOptions.CultureInvariant))
        {
            throw new InvalidDataException(
                $"Custom ADR template '{sourcePath}' contains unsupported placeholder syntax. Use {{{{name}}}} tokens.");
        }

        var template = Parse(
            source,
            cultureName,
            sourcePath);

        // Structural, placeholder and validator checks run before returning
        // anything to a creation workflow. The preview path is only a parser
        // input; loading a template never writes or selects an ADR target.
        string preview;
        try
        {
            preview = AdrMarkdownRenderer.RenderTemplate(
                new AdrTemplateRenderRequest(
                    "Template Preview",
                    template,
                    new Dictionary<string, string>(),
                    Id: 1));
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidDataException(
                $"Custom ADR template '{sourcePath}' is malformed: {exception.Message}",
                exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                $"Custom ADR template '{sourcePath}' is malformed: {exception.Message}",
                exception);
        }
        var validation = AdrValidator.Validate(
            [AdrMarkdownParser.Parse("0001-template-preview.md", preview)]);

        if (!validation.IsValid)
        {
            throw new InvalidDataException(
                $"Custom ADR template '{sourcePath}' is not validator-compatible: "
                + string.Join("; ", validation.Issues.Select(issue => issue.Message)));
        }

        return template;
    }

    private static AdrTemplateDefinition Parse(
        string content,
        string cultureName,
        string sourcePath)
    {
        var lines = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        var sections = new List<AdrTemplateSection>();
        var currentBody = new List<string>();
        string? currentHeading = null;
        var sawTitle = false;
        var sawStatus = false;

        foreach (var line in lines)
        {
            if (!sawTitle)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line != "# {{title}}")
                {
                    throw new InvalidDataException(
                        $"Custom ADR template '{sourcePath}' must start with exactly '# {{{{title}}}}'.");
                }

                sawTitle = true;
                continue;
            }

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                FinishSection();

                var heading = line[3..];
                if (string.IsNullOrWhiteSpace(heading)
                    || heading != heading.Trim()
                    || heading.Contains("{{", StringComparison.Ordinal)
                    || heading.Contains("}}", StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Custom ADR template '{sourcePath}' has an invalid or dynamic level-two heading.");
                }

                if (!sawStatus)
                {
                    if (heading != "Status")
                    {
                        throw new InvalidDataException(
                            $"Custom ADR template '{sourcePath}' must place '## Status' immediately after the title.");
                    }

                    sawStatus = true;
                }
                else if (heading == "Status")
                {
                    throw new InvalidDataException(
                        $"Custom ADR template '{sourcePath}' must not define multiple Status sections.");
                }

                currentHeading = heading;
                continue;
            }

            var leading = line.TrimStart();
            if (leading.StartsWith("# ", StringComparison.Ordinal)
                || leading.StartsWith("## ", StringComparison.Ordinal)
                || leading == "#"
                || leading == "##")
            {
                throw new InvalidDataException(
                    $"Custom ADR template '{sourcePath}' contains a level-one title or malformed level-two heading in a section body.");
            }

            if (currentHeading is null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    throw new InvalidDataException(
                        $"Custom ADR template '{sourcePath}' has text outside a declared section.");
                }

                continue;
            }

            currentBody.Add(line);
        }

        FinishSection();

        if (!sawTitle || !sawStatus)
        {
            throw new InvalidDataException(
                $"Custom ADR template '{sourcePath}' must define '# {{{{title}}}}' and '## Status'.");
        }

        return new AdrTemplateDefinition(
            cultureName,
            sections);

        void FinishSection()
        {
            if (currentHeading is null)
            {
                return;
            }

            var body = string.Join("\n", currentBody).Trim();
            if (currentHeading == "Status")
            {
                if (body is not ("{{status}}" or "Proposed"))
                {
                    throw new InvalidDataException(
                        $"Custom ADR template '{sourcePath}' must set Status to exactly '{{{{status}}}}' or 'Proposed'.");
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(body))
                {
                    throw new InvalidDataException(
                        $"Custom ADR template '{sourcePath}' has an empty '{currentHeading}' section.");
                }

                sections.Add(new AdrTemplateSection(currentHeading, body));
            }

            currentBody.Clear();
            currentHeading = null;
        }
    }
}
