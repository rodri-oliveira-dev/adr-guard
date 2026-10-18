# Security Policy

## Supported versions

Security fixes are applied to the latest released ADR Guard version. Consumers should prefer an exact SemVer tag or commit pin when reproducibility is required and upgrade to a newer release when a security fix is published.

## Reporting a vulnerability

Do **not** open a public issue containing exploit details, credentials, private ADR contents, or other sensitive information.

Use GitHub's private vulnerability reporting / Security Advisory flow for this repository when the **Report a vulnerability** option is available.

If private vulnerability reporting is not available, contact the maintainer through the GitHub profile and request a private reporting channel without including vulnerability details in the public message:

https://github.com/rodri-oliveira-dev

Please include privately:

- affected component and version / Action ref;
- affected execution mode (.NET Tool, MCP server, VS Code extension, container, or GitHub Action);
- impact and attack prerequisites;
- minimal reproduction;
- whether the issue is already public;
- suggested mitigation if known.

## GitHub Action security model

The reusable Action supports deterministic `check` and `index`, opt-in advisory or policy-enforced `review`, and opt-in advisory `impact`. It does not expose `new`, `draft`, `baseline`, or `drift` as Action commands. Review credentials are accepted only through the caller step environment after review is selected explicitly; they are never Action inputs.

Its runtime hardening, token model, workspace write boundaries, anonymous GHCR pull policy, version pinning, and annotation escaping are documented in:

- [docs/github-action-security.md](docs/github-action-security.md)
- [docs/github-action-release.md](docs/github-action-release.md)

## Secret handling

Never place provider API keys or repository secrets in ADR Markdown, workflow inputs, issue reports, or command-line arguments. `check`, `index`, and `impact` do not require provider credentials. For explicit `review`, supply only the selected provider variable through the caller step environment as documented; the Action validates the provider-specific environment before forwarding it to the isolated container.

## Disclosure

Please allow reasonable time for investigation and remediation before public disclosure. Once a fix is released, the maintainer may publish release notes or a security advisory describing the affected versions and mitigation.
