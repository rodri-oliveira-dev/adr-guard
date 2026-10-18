# Support

ADR Guard is an open-source project maintained through GitHub.

## Usage questions and bugs

For reproducible bugs, documentation problems, feature requests, and GitHub Action integration questions, open an issue:

https://github.com/rodri-oliveira-dev/adr-guard/issues

Before opening an issue, include:

- component and version (CLI, MCP, or VS Code extension) or Action ref;
- execution mode (.NET Tool, MCP server, VS Code extension, container, or GitHub Action);
- operating system / runner;
- the command or relevant workflow fragment;
- expected behavior;
- actual behavior and exit code;
- sanitized logs when useful.

Do not include API keys, tokens, private ADR content, credentials, or other secrets.

## Releases

CLI and MCP release notes and downloadable package assets are available at:

https://github.com/rodri-oliveira-dev/adr-guard/releases

The CLI and MCP server use separate NuGet packages and version streams. The VS Code extension uses its own version stream: install the published version from the Visual Studio Marketplace, or download a validated release-candidate VSIX from the extension workflow artifacts, retained for 21 days.

## Security reports

Do not disclose an unpatched vulnerability in a public support issue. Follow [SECURITY.md](SECURITY.md) instead.

## Marketplace

The [ADR Guard GitHub Action](https://github.com/marketplace/actions/adr-guard-architecture-decision-validator) and [ADR Guard for VS Code](https://marketplace.visualstudio.com/items?itemName=rodrioliveira.adr-guard) have public Marketplace listings. The extension Marketplace release and workflow-produced VSIX candidates may have different versions; check the [compatibility matrix](docs/compatibility.md) before reporting a version-specific problem.
