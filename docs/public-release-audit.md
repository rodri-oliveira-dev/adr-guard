# Public release and distribution audit

[Português (Brasil)](public-release-audit.pt-BR.md)

This document records the release/distribution reconciliation performed for issue #80 on **2026-09-27**. It separates public artifacts that are mechanically verifiable from the GitHub Marketplace publication step that still requires repository-owner action.

## Verified release snapshot

At the start of this audit:

| Surface | Verified state |
| --- | --- |
| GitHub Release | `v1.1.6`, published from commit `1b2a75207801526a7564c706b40a92f0eb2ff7a5` |
| Release workflow | Run `36356961234` completed successfully; NuGet.org, GitHub Packages, containers, Action tags and GitHub Release jobs all succeeded |
| Moving Action ref | `refs/tags/v1` resolves to `1b2a75207801526a7564c706b40a92f0eb2ff7a5`, the same commit as `v1.1.6` |
| NuGet.org | Release pipeline published `RodriOliveira.AdrGuard 1.1.6`; CI now installs the latest public GitHub Release version directly from NuGet.org and exercises `new`, `check` and `index` |
| GitHub Packages | The `v1.1.6` release job published successfully. Consumption remains an authenticated GitHub Packages path, as documented in the README |
| GHCR | Release pipeline published and verified exact/minor/major/latest images; CI now pulls the latest exact public tag and moving major tag and smoke-tests both |
| Docker Hub | Release pipeline published and verified the same release; CI now pulls the latest exact public tag and moving major tag and smoke-tests both |
| GitHub Action | Public `@v1` is exercised in isolated consumer fixtures for `check` and `index`; `review` is published on the `v1` line since `v1.1.6` |
| GitHub Marketplace | **No canonical public ADR Guard Marketplace listing has been verified.** Do not claim Marketplace availability yet |

Public source links:

- GitHub Releases: https://github.com/rodri-oliveira-dev/adr-guard/releases
- `v1.1.6`: https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v1.1.6
- NuGet.org: https://www.nuget.org/packages/RodriOliveira.AdrGuard
- GitHub Packages: https://github.com/rodri-oliveira-dev?tab=packages
- GHCR package: https://github.com/rodri-oliveira-dev/adr-guard/pkgs/container/adr-guard
- Docker Hub: https://hub.docker.com/r/rodrigodotnet/adr-guard
- Release run #29: https://github.com/rodri-oliveira-dev/adr-guard/actions/runs/36356961234

## Moving version versus repository baseline

`src/AdrGuard/AdrGuard.csproj` intentionally keeps `VersionPrefix=1.1.0`. The release workflow uses that value as the **release-series baseline**, then compares it with immutable release/reservation tags and increments the patch for subsequent releases.

Therefore:

- `VersionPrefix` is not the current public package version;
- local CI packaging may intentionally produce `1.1.0` for deterministic baseline tests;
- the latest public patch must be obtained from the GitHub Releases/NuGet public surfaces;
- documentation should describe first-available feature versions but should not hard-code a moving “current release” patch unless it is an audit snapshot.

This avoids creating an unnecessary release solely to make source metadata look like the latest patch.

## Future release trigger policy

The `v1.1.6` snapshot above was produced by the previous automatic post-CI release trigger. PR #86 changes the publication policy for future releases: merging to `main` or obtaining a green CI result **does not publish artifacts**.

Future publication starts only when an authorized maintainer manually runs **Actions → Release → Run workflow** with the `main` branch selected. The `Release` workflow uses `workflow_dispatch` only, rejects non-`main` dispatches, verifies that the exact selected `github.sha` already has a completed successful `CI` push run, binds the release to that commit, and re-runs build/tests/package smoke checks before NuGet, package, container, Action-tag or GitHub Release publication begins.

## Continuous public-artifact verification

`scripts/public-distribution-smoke-test.sh` converts the audit into a repeatable CI gate. It:

1. resolves the latest stable GitHub Release dynamically;
2. verifies the moving `vMAJOR` Action tag resolves to the same commit as the latest exact release tag;
3. installs that exact public version from NuGet.org with cache disabled;
4. runs public-tool `new`, `check` and `index` flows in a clean temporary directory;
5. pulls and runs the exact and moving-major GHCR images;
6. pulls and runs the exact and moving-major Docker Hub images;
7. verifies the moving-major container tag matches the exact latest release for the runner platform.

The existing GitHub Action contract job separately invokes the real `rodri-oliveira-dev/adr-guard@v1` reference in isolated consumer fixtures, exercising `check` and `index` without relying on the local Action source.

## Marketplace status and manual blocker

A fresh public search during this audit did not produce a verifiable ADR Guard Marketplace listing, and no canonical Marketplace URL is recorded in repository evidence. The repository therefore remains intentionally explicit: **Marketplace publication is not complete**.

The remaining work cannot be truthfully automated by this PR. An authorized repository/account owner must use GitHub's Marketplace publication flow, satisfy any account agreement/2FA/name-validation requirements shown by GitHub, select an eligible release, publish the Action, then verify the resulting page while logged out.

Only after that manual step should the repository:

- record the exact canonical `github.com/marketplace/actions/...` URL;
- update README/consumer guides with the live listing;
- rerun the independent `poc-arquitetura` consumer against the published `@v1` if issue #49's stricter independent-repository criterion is to be closed;
- close the Marketplace roadmap criteria in #49/#50.

Until then, the absence of a Marketplace URL is deliberate and truthful.

## Historical roadmap reconciliation

- #49 was closed while its own evidence still said Marketplace publication and the independent post-release `@v1` rerun were pending. It should remain/reopen as the owner-only Marketplace follow-up.
- #50 was closed with #49 and the Marketplace global acceptance criteria unchecked. It should remain/reopen until #49 is genuinely complete.
- #59 delivered the `v1.1.0` template-generation roadmap and subsequent public releases prove the coordinated release path works. Its stale unchecked bookkeeping is cross-referenced from #80 rather than used as evidence that the implementation is missing.
- #73 correctly separated published `@v1` state from Marketplace publication.
- #74 correctly treated public artifact verification as a post-merge gate; current release automation has since published the `v1.1.x` line successfully.

## Promotion rule

Promote only what can be consumed now:

- CLI/.NET Tool: public;
- GitHub Release: public;
- GHCR and Docker Hub images: public;
- reusable `@v1` Action: public;
- GitHub Marketplace listing: **not yet verified/publicly evidenced**.

The CI public-distribution gate is the machine-verifiable source for the first four. Marketplace remains a manual, separately evidenced publication milestone.
