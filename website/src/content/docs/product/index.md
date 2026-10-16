---
title: Product documentation
description: Choose the ADR Guard interface and validation workflow that fits your environment.
sidebar:
  order: 1
---

ADR Guard keeps architecture judgment with people while automating repeatable document checks. Start with the CLI, then add only the integrations your workflow needs.

## Interfaces

| Interface | Use it when | Trust and execution boundary |
| --- | --- | --- |
| [CLI](./cli/) | You need the complete local command surface | Runs locally with explicit arguments and configuration |
| [GitHub Action](./github-action/) | Pull requests should validate or review ADR changes | Runs in GitHub Actions with workflow permissions and pinned inputs |
| [VS Code extension](./vscode-extension/) · [Visual guide](./vscode-visual-guide/) | Contributors want visual ADR creation/editing, explorer, commands, and diagnostics in the editor | Workspace-hosted; trusted local file workspaces only; invokes an existing compatible CLI |
| [MCP server](./mcp/) | Compatible agents need structured, read-only ADR Guard tools | MCP 1.0.0 candidate via local package/source build; one bounded repository per `stdio` process; no official MCP distribution yet |
| [Container](./containers/) | You want an isolated, reproducible CLI runtime | Mount only required files and pass configuration deliberately |
| [Agent Skills](../skills/) | You want a coding agent to guide ADR decisions and CLI workflows | Portable agent instructions; no bundled CLI or automatic architectural approval |

## Core workflows

- [Create canonical ADRs](./creation/) with Minimal, Extended, or constrained custom templates. Compatible VS Code forms use the CLI 1.7.0 structured creation and editing contracts when those separately versioned artifacts are published.
- [Validate relationships](./relationships/), changed files, and [diagnostic baselines](./incremental-validation/).
- Use [Architecture Impact Analysis](./architecture-impact/) to identify decisions that explicit ADR-to-code mappings associate with changed files. The result is a review prompt, not an architecture violation.
- Use [Architectural Drift](./architectural-drift/) to verify explicit ADR-backed fitness rules from bounded evidence. Results distinguish conformance, violation, and insufficient evidence; opt-in enforcement applies only within the implemented deterministic limits.
- Produce [machine-readable and human-readable reports](./reports/).
- Opt into [AI-assisted drafting](./ai-drafting/) or [review](./ai-review/) with explicit providers and security controls.
- Validate MADR 4.0 documents in a [separate opt-in mode](./madr-4/).

## Impact and drift are different

| Capability | Question it answers | What it does not claim |
| --- | --- | --- |
| [Architecture Impact Analysis](./architecture-impact/) | Which ADRs may deserve review because mapped files changed? | It does not interpret a change as non-compliance or perform unrestricted semantic code analysis. |
| [Architectural Drift](./architectural-drift/) | Does available evidence satisfy an explicit fitness rule linked to an ADR? | It does not infer undeclared rules, turn missing evidence into a pass, or approve the underlying decision. |

Both workflows keep evidence traceable and architecture judgment human-owned. Document validation checks whether ADRs follow their declared contracts; technical evidence assesses only the rules and scope explicitly configured; people still decide whether an ADR is accepted and whether a rule represents the intended architecture.

## AI and human confirmation

Drafting and review send selected context to the configured provider. Provider output is untrusted, bounded, parsed, and presented for human evaluation. It does not approve decisions, edit accepted history automatically, or replace security and architecture review.

## MCP status

The standalone MCP 1.0.0 server and optional provider in the VS Code 1.1.0 candidate are implemented and tested. They can be installed from a local package or built from source and expose seven read-only tools over `stdio`. No MCP NuGet package, container, installer, Marketplace extension release containing the provider, or other official MCP distribution has been published. Do not treat source availability as release availability.

Use the [MCP overview](./mcp/), [configuration guide](./mcp-configuration/), [available tools](./mcp-tools/), and [security model](./mcp-security/) for the implemented contract and local-development instructions.

For command-level details, use the [CLI reference](./cli/) and preserve the actual installed version when troubleshooting.
