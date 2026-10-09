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
| [VS Code extension](./vscode-extension/) | Contributors want explorer, commands, and diagnostics in the editor | Workspace-hosted; trusted local file workspaces only; invokes an existing CLI |
| [Container](./containers/) | You want an isolated, reproducible CLI runtime | Mount only required files and pass configuration deliberately |

## Core workflows

- [Create canonical ADRs](./creation/) with Minimal, Extended, or constrained custom templates.
- [Validate relationships](./relationships/), changed files, and [diagnostic baselines](./incremental-validation/).
- Produce [machine-readable and human-readable reports](./reports/).
- Opt into [AI-assisted drafting](./ai-drafting/) or [review](./ai-review/) with explicit providers and security controls.
- Validate MADR 4.0 documents in a [separate opt-in mode](./madr-4/).

## AI and human confirmation

Drafting and review send selected context to the configured provider. Provider output is untrusted, bounded, parsed, and presented for human evaluation. It does not approve decisions, edit accepted history automatically, or replace security and architecture review.

## MCP status

The repository does not currently publish a standalone ADR Guard MCP server. Do not configure or depend on one based on this portal. The supported public surfaces are the CLI, GitHub Action, container, and VS Code extension documented here.

For command-level details, use the [CLI reference](./cli/) and preserve the actual installed version when troubleshooting.
