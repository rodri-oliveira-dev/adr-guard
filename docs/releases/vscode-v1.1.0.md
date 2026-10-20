# ADR Guard for VS Code 1.1.0 — Published

[Português (Brasil)](vscode-v1.1.0.pt-BR.md)

The independently versioned **ADR Guard for VS Code 1.1.0** is published in the [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=rodrioliveira.adr-guard). Availability was reverified on 2026-10-20. This Marketplace state is independent from CLI and MCP releases and does not imply that the repository's VSIX workflow published it or that a Git tag/GitHub Release exists.

## Highlights

- Visual ADR browsing, creation, editing, settings, validation, architecture impact and drift, diagnostic baselines, enriched catalogs, and optional AI-assisted drafting/review.
- Optional MCP registration provider, disabled by default, for a compatible server installed separately on the workspace extension host.
- Bilingual UI, explicit execution and confirmation boundaries, workspace trust enforcement, cancellation, stale-response protection, and strict versioned JSON adapters.
- Problems diagnostics navigate to optional CLI-provided line and column locations and retain document-start fallback for older CLIs or file-only findings.

## Fixes and internal improvements

- Preserves repository-relative impact and drift operands expected by the CLI.
- Avoids requiring the ADR directory for unrelated analysis/review workflows.
- Keeps domain parsing, validation, policy, impact, and drift decisions in the .NET CLI/Core; the extension remains a workspace-host adapter.
- Packages a bundled Node entry with development files, source maps, fixtures, credentials, and .NET binaries excluded from the VSIX.

## Compatibility and requirements

- VS Code `1.101.0+`, a trusted local `file:` workspace, and the Node workspace extension host.
- A separately installed compatible CLI. CLI `1.7.0+` supplies schema `1.0` visual create/edit; published CLI `1.8.0` supports the documented architecture, governance, and AI workflows.
- CLI `1.9.0` remains an unpublished candidate that adds precise diagnostic locations; the extension falls back safely with older CLIs.
- MCP is optional. Published MCP `1.0.0` is compatible; MCP `1.0.1` is an independent unpublished candidate with unchanged schema `1.0` tool contracts.

## Behavior changes

The new architecture, governance, AI, and MCP surfaces run only after explicit user actions. Enabling MCP registers definitions but does not start a process during extension activation. AI remains optional and advisory; it cannot approve ADRs or establish compliance.

## Known limitations

- The extension does not bundle, download, or update the CLI or MCP server.
- Virtual and untrusted workspaces are unsupported. Remote workspaces require executables and paths on the remote extension host.
- Existing screenshots cover the shared visual interface but do not serve as evidence for every 1.1.0 architecture, AI, or MCP screen.

## Install or update

Install the published extension from the Extensions view by searching for **ADR Guard**, or run:

```shell
code --install-extension rodrioliveira.adr-guard
```

Confirm that VS Code reports version `1.1.0`. Install the CLI and optional MCP server separately in the workspace-host environment.

## Validated VSIX path

The manual [VS Code VSIX Package workflow](https://github.com/rodri-oliveira-dev/adr-guard/actions/workflows/vscode-extension-release.yml) still validates and builds `adr-guard-1.1.0.vsix`, then retains the `adr-guard-vscode-1.1.0` artifact for 21 days (subject to repository limits). It does **not** contain Marketplace credentials or execute `vsce publish`; a successful workflow artifact is not publication evidence.

## Publication follow-up checklist

- Marketplace `rodrioliveira.adr-guard` reports `1.1.0`: verified 2026-10-20.
- Keep `extensions/vscode/package.json` at `1.1.0`; do not increment it as part of CLI/MCP release preparation.
- Keep VSIX CI/package workflows artifact-only and document any future Marketplace publication path separately.
- Do not infer a Git tag, GitHub Release, or CLI/MCP availability from the Marketplace listing; verify each channel independently.

See the [extension guide](../../extensions/vscode/README.md), [compatibility matrix](../compatibility.md), and [VSIX packaging guide](../../extensions/vscode/PUBLISHING.md).
