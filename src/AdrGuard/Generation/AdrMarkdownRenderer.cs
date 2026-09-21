using System.Globalization;
using System.Text;

namespace AdrGuard.Generation;

internal static class AdrMarkdownRenderer
{
    // This is the original draft formatter, moved without changing its whitespace
    // or Environment.NewLine behavior. Template rendering has a separate stable
    // LF contract; default draft remains backward-compatible on every host.
    internal static string RenderDefaultDraft(
        string title,
        AdrGenerationResult generated)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(generated);

        var builder = new StringBuilder();

        builder
            .Append("# ")
            .AppendLine(title.Trim())
            .AppendLine()
            .AppendLine("## Status")
            .AppendLine()
            .AppendLine("Proposed")
            .AppendLine()
            .AppendLine("## Context")
            .AppendLine()
            .AppendLine(generated.Context?.Trim() ?? string.Empty)
            .AppendLine()
            .AppendLine("## Decision")
            .AppendLine()
            .AppendLine(generated.Decision?.Trim() ?? string.Empty)
            .AppendLine()
            .AppendLine("## Consequences")
            .AppendLine()
            .AppendLine(generated.Consequences?.Trim() ?? string.Empty);

        return builder.ToString();
    }

    // Exact, documented placeholder names. Custom values are never interpreted
    // as templates and unknown/malformed tokens in template data are errors.
    internal static IReadOnlyList<string> SupportedPlaceholders { get; } =
    [
        "title",
        "id",
        "status",
        "context",
        "decision",
        "consequences",
        "guidance-context",
        "guidance-decision",
        "guidance-consequences",
    ];

    internal static string RenderTemplate(AdrTemplateRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);
        ArgumentNullException.ThrowIfNull(request.Template);
        ArgumentNullException.ThrowIfNull(request.Substitutions);

        if (request.Title.IndexOfAny(['\r', '\n']) >= 0)
        {
            throw new ArgumentException(
                "An ADR title must be a single line.",
                nameof(request));
        }

        if (request.Id is <= 0 or > 9999)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "An ADR template ID must be between 1 and 9999.");
        }

        var guidance = GetGuidance(request.Template.CultureName);
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = EscapeInlineMarkdown(request.Title.Trim()),
            ["status"] = "Proposed",
            ["context"] = string.Empty,
            ["decision"] = string.Empty,
            ["consequences"] = string.Empty,
            ["guidance-context"] = guidance.Context,
            ["guidance-decision"] = guidance.Decision,
            ["guidance-consequences"] = guidance.Consequences,
        };

        if (request.Id is { } id)
        {
            values["id"] = id.ToString("D4", CultureInfo.InvariantCulture);
        }

        foreach (var pair in request.Substitutions)
        {
            if (!SupportedPlaceholders.Contains(pair.Key, StringComparer.Ordinal)
                || pair.Key.StartsWith("guidance-", StringComparison.Ordinal)
                || pair.Key is "title" or "id" or "status")
            {
                throw new ArgumentException(
                    $"Unsupported or reserved template substitution '{pair.Key}'.",
                    nameof(request));
            }

            values[pair.Key] = NormalizeNewlines(
                pair.Value
                    ?? throw new ArgumentException(
                        $"Template substitution '{pair.Key}' cannot be null.",
                        nameof(request)));
        }

        var sections = request.Template.Sections
            ?? throw new ArgumentException(
                "A template must provide canonical sections.",
                nameof(request));

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var builder = new StringBuilder()
            .Append("# ")
            .Append(values["title"])
            .Append("\n\n## Status\n\nProposed\n");

        foreach (var section in sections)
        {
            if (section is null)
            {
                throw new ArgumentException(
                    "Template sections cannot be null.",
                    nameof(request));
            }

            ValidateHeading(section.Heading);
            if (!seen.Add(section.Heading))
            {
                throw new InvalidOperationException(
                    $"Duplicate template section '{section.Heading}'.");
            }

            var body = Substitute(
                section.BodyTemplate
                    ?? throw new ArgumentException(
                        "A template section body cannot be null.",
                        nameof(request)),
                values);

            GeneratedAdrStructureGuard.ValidateTemplateContent(
                section.Heading,
                body);

            builder
                .Append("\n## ")
                .Append(section.Heading)
                .Append("\n\n")
                .Append(body.Trim())
                .Append('\n');
        }

        foreach (var required in new[] { "Context", "Decision", "Consequences" })
        {
            if (!seen.Contains(required))
            {
                throw new InvalidOperationException(
                    $"Template is missing required canonical section '{required}'.");
            }
        }

        return builder.ToString();
    }

    private static (string Context, string Decision, string Consequences)
        GetGuidance(string cultureName) => cultureName switch
        {
            "en-US" => (
                "Describe the context and constraints.",
                "Describe the decision and its rationale.",
                "Describe the consequences and trade-offs."),
            "pt-BR" => (
                "Descreva o contexto e as restrições.",
                "Descreva a decisão e sua justificativa.",
                "Descreva as consequências e os compromissos."),
            _ => throw new ArgumentException(
                $"Template guidance culture '{cultureName}' is not supported. Use 'en-US' or 'pt-BR'.",
                nameof(cultureName)),
        };

    private static void ValidateHeading(string heading)
    {
        if (string.IsNullOrWhiteSpace(heading)
            || heading.Equals("Status", StringComparison.OrdinalIgnoreCase)
            || heading is not { Length: <= 120 }
            || heading.Any(character =>
                !char.IsLetterOrDigit(character)
                && character is not (' ' or '-' or '/' or '(' or ')' or '_')))
        {
            throw new InvalidOperationException(
                $"Invalid or reserved template section heading '{heading}'.");
        }

        // Validator-sensitive headings are invariant, not translated.
        foreach (var canonical in new[] { "Context", "Decision", "Consequences" })
        {
            if (heading.Equals(canonical, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(heading, canonical, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Canonical template heading must be '{canonical}' exactly.");
            }
        }
    }

    private static string Substitute(
        string template,
        Dictionary<string, string> values)
    {
        var builder = new StringBuilder();
        var cursor = 0;

        while (cursor < template.Length)
        {
            var open = template.IndexOf("{{", cursor, StringComparison.Ordinal);
            var close = template.IndexOf("}}", cursor, StringComparison.Ordinal);

            if (close >= 0 && (open < 0 || close < open))
            {
                throw new InvalidOperationException(
                    "Unexpected closing template placeholder delimiter '}}'.");
            }

            if (open < 0)
            {
                builder.Append(template, cursor, template.Length - cursor);
                break;
            }

            builder.Append(template, cursor, open - cursor);

            if (close < 0 || template.IndexOf("{{", open + 2, close - open - 2, StringComparison.Ordinal) >= 0)
            {
                throw new InvalidOperationException(
                    "Unclosed or nested template placeholder delimiter '{{'.");
            }

            var name = template[(open + 2)..close];
            if (!values.TryGetValue(name, out var value))
            {
                throw new InvalidOperationException(
                    $"Unknown template placeholder '{{{{{name}}}}}'.");
            }

            // Single pass: substitution values containing {{...}} remain literal.
            builder.Append(value);
            cursor = close + 2;
        }

        return NormalizeNewlines(builder.ToString());
    }

    private static string NormalizeNewlines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

    private static string EscapeInlineMarkdown(string text)
    {
        var builder = new StringBuilder(text.Length);

        foreach (var character in text)
        {
            if (character is '\\' or '`' or '*' or '_' or '{' or '}' or '['
                or ']' or '(' or ')' or '#' or '+' or '-' or '!' or '<' or '>')
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
