# MCP distribution and release prerequisites

[Português (Brasil)](mcp-release.pt-BR.md) · [MCP overview](mcp.md)

The `MCP Release` workflow (`.github/workflows/mcp-release.yml`) is independent from the CLI release. It accepts one stable SemVer, runs only when manually dispatched from `main`, requires successful CI for the selected commit, and resolves package, manifest, tag, GitHub Release, and Registry versions from that single input. Do not dispatch it before the setup below is complete.

MCP `1.0.0` has been published to [NuGet.org](https://www.nuget.org/packages/RodriOliveira.AdrGuard.Mcp/1.0.0), a [dedicated GitHub Release](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/mcp/v1.0.0), and the MCP Registry. GitHub Packages is a separate, authenticated secondary NuGet feed; its `1.0.0` availability requires the dedicated backfill workflow after this change is merged.

## Required one-time administration

1. On NuGet.org, ensure `rodri-oliveira-dev` owns or can create `RodriOliveira.AdrGuard.Mcp`.
2. In that package's **Trusted Publishing** settings, add a GitHub Actions policy for owner `rodri-oliveira-dev`, repository `adr-guard`, workflow `mcp-release.yml`, and environment `mcp-release`. A new package may require NuGet.org's pending-policy activation flow; the CLI package policy does not automatically cover this Package ID.
3. In GitHub, create the protected `mcp-release` environment, restrict it to `main`, and configure required reviewers for irreversible publication jobs.
4. Confirm branch protection requires the complete `CI` workflow on `main`, and permit Actions to create `mcp/v*` and `mcp-release-reservation/v*` tags and Releases.

No PAT or persistent MCP/NuGet publishing token is required. NuGet and the MCP Registry receive `id-token: write` only in their protected publishing jobs.

## Ordering, artifacts, and recovery

The workflow builds one framework-dependent `.nupkg`, validates its embedded README marker and runtime dependencies, generates SHA-256 checksums, and installs it from an isolated source on Windows, Linux, and macOS. It reserves the version, publishes with NuGet OIDC, verifies and installs the public package, and publishes the same checksum-verified candidate to GitHub Packages using the workflow's scoped `GITHUB_TOKEN`. The primary flow also creates `mcp/v<version>` and the GitHub Release, then authenticates to the official Registry with GitHub OIDC. A failure in the secondary GitHub Packages job does not bypass NuGet.org verification or the GitHub Release/Registry gates; the release summary runs even if a distribution job fails and shows the actual status of each channel.

Reruns preserve the original version and SHA. The reservation step distinguishes HTTP `404` (create a new tag) from HTTP `200` (verify the existing tag's exact commit) and fails closed on authorization, transport, or server errors. Both GitHub API responses are capped at 64 KiB, and the short-lived GitHub token is supplied through curl's standard-input configuration, never a process argument. Existing reservation/release tags must point to that SHA. Existing NuGet and Registry versions must prove matching metadata or the workflow fails closed. Missing later stages can resume; immutable artifacts and tags are never overwritten or moved.

Release assets are the MCP `.nupkg`, `SHA256SUMS`, validated `server.json`, and MCP-specific notes. The package is not a self-contained native binary. Registry preview availability and schema changes remain external risks, and PR/push workflows never publish.

## GitHub Packages: existing release backfill

Since MCP `1.0.0` was released before GitHub Packages distribution was added, **do not rerun `MCP Release` from a newer commit to backfill version `1.0.0`**: its immutable release and reservation tags belong to the original commit. Instead, after merging this PR and obtaining successful `main` CI, manually run **MCP GitHub Packages Backfill** (`.github/workflows/mcp-github-packages-backfill.yml`) from `main` with `version=1.0.0`, approving the protected `mcp-release` environment if prompted.

The backfill verifies the published release tag and its original SHA, downloads the original GitHub Release `.nupkg` and `SHA256SUMS`, validates checksum and NuGet package metadata against that commit, and checks the existing NuGet.org package's matching ID/version/commit metadata. Only then does it publish the original package to `https://nuget.pkg.github.com/rodri-oliveira-dev/index.json` with `packages: write` and `NUGET_API_KEY` in the environment (never on the command line). It first queries the authenticated GitHub NuGet feed: an existing version succeeds only if its downloaded `.nupkg` has the **exact original SHA-256**; absent versions are published without `--skip-duplicate` and verified by downloading and hashing the registry copy. Conflict, mismatch, unauthorized response, or inconclusive availability fails closed. NuGet.org can repository-sign a package, so raw bytes are not assumed identical across registries. It does not rebuild, retag, publish a new NuGet.org version, or alter the MCP Registry. Future MCP releases use the normal `MCP Release` pipeline for GitHub Packages automatically.

GitHub Packages is an **authenticated secondary feed**. Consumers need appropriate GitHub package access/credentials; the official NuGet.org package remains the recommended installation source. Package visibility and repository linkage in GitHub Packages must be confirmed after first publication. The VS Code extension is distributed separately through the Visual Studio Marketplace and does not appear in GitHub Packages.
