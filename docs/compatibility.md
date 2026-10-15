# ADR format and validation compatibility

Document format and validation rigor are independent. Existing commands default to `canonical` plus `legacy`, preserving the published behavior and exit codes.

| Profile | Lifecycle | Supersession text | Placeholders | Metadata |
| --- | --- | --- | --- | --- |
| `legacy` | Four historical statuses | Off | Off | Optional, unchecked |
| `advisory` | Historical statuses | Off | Warning | Optional, unchecked |
| `standard` | Adds `Rejected` | On | Warning | Validated |
| `strict` | Adds `Rejected` | On | Error | Validated |

Use `adr-format: canonical` or `madr-4` independently with `validation-profile: legacy|advisory|standard|strict`. Explicit lifecycle, filename, placeholder, and metadata settings refine the selected profile. Baseline fingerprints retain diagnostic code, path, and message identity; warnings do not change the exit code. JSON adds `severity` only for warnings, and SARIF emits their standard `warning` level.

## Integration versions

| Surface | Compatibility boundary |
| --- | --- |
| Published CLI | Consult the installed `adr-guard --version` and the command's `--help`; extension, MCP, and CLI versions are not interchangeable. |
| VS Code extension | Requires VS Code `1.101.0` or newer. Visual form contracts require CLI 1.7.0; development-branch analysis workflows probe each CLI command before use. |
| MCP server | Currently source-only and built for .NET 10. Tool schemas are `1.0`; no independently versioned MCP package or official distribution has been published. |

Source implementation, merge into the default branch, and publication to a distribution channel are separate states. Documentation that describes a development branch does not establish release availability.

