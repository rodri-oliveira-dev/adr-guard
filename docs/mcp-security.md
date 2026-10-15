# ADR Guard MCP security

[Português (Brasil)](mcp-security.pt-BR.md) · [Overview](mcp.md) · [Configuration](mcp-configuration.md) · [Tools](mcp-tools.md)

The MCP adapter is a local, read-only boundary around existing ADR Guard services. Its controls reduce accidental scope expansion; they do not make repository content trusted or replace the client agent's permission model.

## Trust and process boundary

- The server uses only local `stdio`; it opens no listening network endpoint and needs no credential.
- The repository root and ADR directory are fixed by explicit startup arguments and validated before serving requests.
- Tool paths are normalized, must remain repository-relative and contained, and cannot traverse symlinks/reparse points outside the root.
- No request is assembled into a shell command. The server does not execute Markdown, configuration values, evidence, or provider output.
- Operational logging uses standard error, records tool names and exception types rather than payloads, and sanitizes repository roots from returned operational errors.

When registered by the VS Code extension, additional controls apply: `workspace.isTrusted` must be true, folders must use the local `file:` scheme, the configured server must be a regular non-symlink file outside every workspace, and a workspace-scoped server path is ignored. Arguments are passed as discrete values. VS Code owns process startup and shutdown; extension activation itself starts no MCP process.

## Least privilege and workspace isolation

Use one MCP process per repository. In a multi-root VS Code workspace the provider emits one labeled definition per enabled folder, each with its own `cwd`, `--repository`, and `--adr-directory`. It never grants one process a global root spanning all folders.

Enable only the folders an agent needs and review the client permission prompt for every tool. Disable `adrGuard.mcp.enabled` when agent access is not required. Disabling removes the definition through the VS Code provider lifecycle without affecting traditional CLI features.

The current tools have no write side effects. They cannot create/update baselines, indexes, ADRs, mappings, rules, or Git state. `analyze_impact` reads local Git state without fetching; `verify_architecture` consumes existing evidence without running its producer.

## Untrusted content and bounded resources

ADRs, Markdown, manifests, evidence, Git output, and configuration are untrusted input. The server:

- marks returned Markdown and search excerpts as untrusted;
- limits ADR discovery to 1,000 files and each ADR to 512 KiB;
- limits search text to 256 printable characters and results to at most 100;
- limits evidence inputs to 20 files;
- validates versioned impact, drift, evidence, baseline, and repository configuration contracts through the shared domain services;
- enforces a 1 MiB serialized response ceiling and a configurable 1–300 second operation timeout;
- honors client cancellation and treats malformed, missing, stale, or contradictory evidence conservatively.

A client or agent must not interpret Markdown text as tool instructions. Review proposed follow-up actions and keep the client's own tool approvals enabled.

## Privacy and credentials

ADR Guard MCP performs no model call and requires no API key. It does not need provider credentials from the CLI's separate AI drafting/review features. Do not add secrets to MCP arguments, settings, logs, example files, or client environment entries.

An agent may transmit tool results according to that agent/provider's privacy policy. Before authorizing a request, consider whether ADR content, file names, mappings, or evidence may leave the machine. Configure and audit the agent independently; the local MCP boundary cannot control what a client does with returned data.

## Result semantics

- Impact is advisory and indicates a decision may need review.
- `unknown` and inconclusive drift results are not compliance.
- `policyFailed` reports deterministic enforcement outcome but does not approve or reject an ADR.
- AI interpretation is advisory and cannot waive diagnostics, establish evidence, or change lifecycle status.

Humans remain responsible for architectural decisions, approval, security review, and repository changes.

## Incident and troubleshooting guidance

Stop or disable the client definition if scope appears wrong. Verify the effective repository and ADR directory, the server binary path, workspace trust, remote host, and client permission history. Do not paste credentials or complete sensitive payloads into issue reports. Report suspected vulnerabilities through [SECURITY.md](../SECURITY.md), not a public issue.
