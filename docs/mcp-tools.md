# ADR Guard MCP tools

[Português (Brasil)](mcp-tools.pt-BR.md) · [Overview](mcp.md) · [Configuration](mcp-configuration.md) · [Security](mcp-security.md)

All tools use JSON schema version `1.0`, are read-only, and operate only on the repository fixed when the MCP process starts. Paths in requests and responses are repository-relative. Validation findings, impacted decisions, and drift violations are analysis results—not protocol failures and not architectural approval.

## Tool summary

| Tool | Purpose | Inputs |
| --- | --- | --- |
| `list_adrs` | List bounded ADR metadata in deterministic order. | None. |
| `get_adr` | Read one ADR and its untrusted Markdown. | `identifier` (required). |
| `search_adrs` | Deterministic, case-insensitive AND search. | `query` (required), `limit` (default `20`, range `1`–`100`). |
| `get_adr_relationships` | Return declared supersession/dependency edges and related diagnostics. | `identifier` (optional). |
| `validate_adrs` | Run deterministic ADR validation. | `validationProfile` (optional: `legacy`, `advisory`, `standard`, `strict`). |
| `analyze_impact` | Correlate local Git changes with explicit ADR-to-code mappings. | `baseReference`, `manifestPath` (both required). |
| `verify_architecture` | Evaluate declared fitness rules from bounded evidence. | `rulesPath`; optional `evidencePaths`, `baselinePath`, `policy`. |

## `list_adrs`

Returns `schemaVersion`, `total`, and `adrs`. Each summary contains `identifier`, nullable `title` and `status`, repository-relative `path`, and `format` (`canonical` or `madr-4`). It excludes the generated ADR `README.md`.

Example agent request: “List the ADRs in this repository and group the returned metadata by status.” The MCP tool lists records; the agent performs grouping and prose explanation.

Failures include malformed repository configuration, unreadable files, more than 1,000 ADRs, oversized or invalid UTF-8 documents, and cancellation/timeout.

## `get_adr`

`identifier` accepts a stable ID, numeric ID, or safe path within the configured ADR directory. The result contains `adr`, `content`, and `contentIsUntrusted: true`.

Example input:

```json
{ "identifier": "ADR-0003" }
```

The request fails when the identifier is missing, ambiguous, outside the configured set, longer than 1,024 characters, or contains control characters. Returned Markdown must never be treated as instructions to execute.

## `search_adrs`

`query` contains 1–256 printable characters. Whitespace-separated unique terms are matched case-insensitively with AND semantics against identifier, title, and bounded Markdown. Results contain matched fields and an optional excerpt marked by `excerptsAreUntrusted: true`.

Example input:

```json
{ "query": "authentication token", "limit": 10 }
```

Ordering follows deterministic ADR discovery and stops at `limit`; this is not semantic or AI search.

## `get_adr_relationships`

With no input, inspects all configured ADRs. With `identifier`, limits source relationships and diagnostics to one resolved ADR. The response separates declared `relationships` from validation `issues`. Relationship records include source, type (`superseded-by`, `supersedes`, or `depends-on`), declared target, resolved target when available, and resolution state.

Example input:

```json
{ "identifier": "7" }
```

The server does not infer undeclared edges. Broken, ambiguous, cyclic, inconsistent, or inactive relationships remain visible as diagnostics.

## `validate_adrs`

Runs the same parser, policy factory, and deterministic validator used by ADR Guard. The result includes `valid`, file count, effective policy, and diagnostics with code, message, path, and severity.

Example input:

```json
{ "validationProfile": "standard" }
```

An invalid ADR set is a successful tool result with `valid: false`; it is not silently converted into an MCP transport error. Unknown profiles and malformed configuration fail the call.

## `analyze_impact`

Reads a local Git change inventory from an explicit base reference and a versioned mapping JSON file. It never fetches, checks out, or changes Git state.

Example input:

```json
{
  "baseReference": "origin/main",
  "manifestPath": ".adrguard-impact.json"
}
```

The response contains repository metadata, summary and coverage counts, explicit uncertainty, changed files, affected/unknown decisions, and mapping evidence. Impact means “review this decision,” not “this change violates the decision.” Missing refs, invalid mappings, unsafe paths, and Git failures fail safely.

## `verify_architecture`

Loads a versioned fitness rules manifest, up to 20 already-produced evidence JSON files, and an optional existing drift baseline. `policy` defaults to `advisory` and accepts only `advisory` or `enforce`.

Example input:

```json
{
  "rulesPath": ".adrguard-fitness.json",
  "evidencePaths": ["artifacts/architecture-evidence.json"],
  "baselinePath": ".adrguard-drift-baseline.json",
  "policy": "advisory"
}
```

The result preserves `pass`, `violation`, `unknown`, and `not-applicable` findings, confidence, baseline state, evidence, `inconclusive`, and `policyFailed`. Missing or contradictory evidence is never a pass. Enforcement reports deterministic policy failure in the structured result but does not modify the repository. The tool does not run builds, tests, MSBuild, or evidence-producing scripts.

## Error and side-effect contract

- Invalid parameters are MCP invalid-parameter errors.
- Bounded operational/configuration failures become sanitized MCP errors; absolute repository paths are replaced in messages.
- Client cancellation is propagated. Server timeout becomes an explicit timeout error.
- Responses larger than 1 MiB are rejected with guidance to narrow the request.
- No tool creates, edits, deletes, approves, or changes the status of an ADR. No tool updates a baseline, index, mapping, rules file, or Git state.
