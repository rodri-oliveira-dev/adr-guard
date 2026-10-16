# MCP distribution and first-release prerequisites

[Português (Brasil)](mcp-release.pt-BR.md) · [MCP overview](mcp.md)

The `MCP Release` workflow (`.github/workflows/mcp-release.yml`) is independent from the CLI release. It accepts one stable SemVer, runs only when manually dispatched from `main`, requires successful CI for the selected commit, and resolves package, manifest, tag, GitHub Release, and Registry versions from that single input. Do not dispatch it before the setup below is complete.

The repository is prepared for the `1.0.0` candidate, but that version is not published. Treat the contract as operationally stable only after the protected workflow completes package verification, public installation, Registry publication, and post-publication checks.

## Required one-time administration

1. On NuGet.org, ensure `rodri-oliveira-dev` owns or can create `RodriOliveira.AdrGuard.Mcp`.
2. In that package's **Trusted Publishing** settings, add a GitHub Actions policy for owner `rodri-oliveira-dev`, repository `adr-guard`, workflow `mcp-release.yml`, and environment `mcp-release`. A new package may require NuGet.org's pending-policy activation flow; the CLI package policy does not automatically cover this Package ID.
3. In GitHub, create the protected `mcp-release` environment, restrict it to `main`, and configure required reviewers for irreversible publication jobs.
4. Confirm branch protection requires the complete `CI` workflow on `main`, and permit Actions to create `mcp/v*` and `mcp-release-reservation/v*` tags and Releases.

No PAT or persistent MCP/NuGet publishing token is required. NuGet and the MCP Registry receive `id-token: write` only in their protected publishing jobs.

## Ordering, artifacts, and recovery

The workflow builds one framework-dependent `.nupkg`, validates its embedded README marker and runtime dependencies, generates SHA-256 checksums, and installs it from an isolated source on Windows, Linux, and macOS. It reserves the version, publishes with NuGet OIDC, verifies and installs the public package, creates `mcp/v<version>` and the GitHub Release, then authenticates to the official Registry with GitHub OIDC.

Reruns preserve the original version and SHA. Existing reservation/release tags must point to that SHA. Existing NuGet and Registry versions must prove matching metadata or the workflow fails closed. Missing later stages can resume; immutable artifacts and tags are never overwritten or moved.

Release assets are the MCP `.nupkg`, `SHA256SUMS`, validated `server.json`, and MCP-specific notes. The package is not a self-contained native binary. Registry preview availability and schema changes remain external risks, and PR/push workflows never publish.
