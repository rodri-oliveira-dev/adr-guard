# GitHub Marketplace publication checklist

Checked against the official GitHub documentation on **2026-09-21**.

Official references:

- https://docs.github.com/en/actions/how-tos/create-and-publish-actions/publish-in-github-marketplace
- https://docs.github.com/en/actions/reference/workflows-and-actions/metadata-syntax
- https://docs.github.com/en/actions/how-tos/create-and-publish-actions/release-and-maintain-actions

## Listing identity

Proposed Marketplace name:

`ADR Guard - Architecture Decision Validator`

Description:

`Validate and index Architecture Decision Records (ADRs) with deterministic checks and GitHub file annotations.`

Branding:

- icon: `shield`
- color: `purple`
- recommended primary category: **Code quality**
- recommended secondary category: **Continuous integration**

The category selections are made in the GitHub release/Marketplace UI rather than in `action.yml`.

A web search performed on 2026-09-21 found no existing Marketplace listing with the exact proposed name. GitHub performs the authoritative uniqueness check when the listing is prepared; publication must stop if the UI reports a name collision.

## Repository requirements

Current repository state:

- repository: `rodri-oliveira-dev/adr-guard`
- visibility: public
- root metadata file: `action.yml`
- alternate root `action.yaml`: not used
- license: MIT
- consumer documentation: English and pt-BR
- support policy: [../SUPPORT.md](../SUPPORT.md)
- security policy: [../SECURITY.md](../SECURITY.md)

GitHub's Action-specific Marketplace requirements state that the Action must live in a public repository, the repository must contain one root Action metadata file, and the metadata `name` must be unique.

This repository contains the CLI, container build, tests, scripts, and documentation required to build, publish, validate, and operate ADR Guard. No additional Action metadata file is used at the repository root.

## Release readiness

The Action release flow implemented in this repository:

- validates the commit in CI;
- publishes the .NET Tool and container artifacts;
- verifies the GHCR runtime image;
- creates an immutable `vMAJOR.MINOR.PATCH` Action tag;
- advances the compatible `vMAJOR` tag only after successful publication;
- creates the GitHub Release after the Action tags exist.

The Marketplace listing must select a release-tagged Action version. Do not publish from a development branch.

The first supported Marketplace consumer reference is intended to be `@v1`. Until a real `v1` tag exists and is verified, documentation must continue to describe it as forthcoming.

## Manual owner gates

The following steps require the repository/account owner in GitHub's web UI and are intentionally not automated:

- Confirm that the proposed Action name passes GitHub's live Marketplace uniqueness validation.
- Accept the **GitHub Marketplace Developer Agreement** for the account that owns the repository if it has not already been accepted.
- Ensure the publishing account satisfies GitHub's release authentication requirements, including two-factor authentication (2FA).
- Open the root `action.yml` after a release tag exists and use the Marketplace publication banner / draft release flow.
- Select **Publish this Action to the GitHub Marketplace**.
- Resolve every metadata warning until GitHub displays its successful validation state.
- Select **Code quality** as the primary category and **Continuous integration** as the secondary category when those categories are offered by the current UI.
- Select the exact release tag being published.
- Review the generated Marketplace preview, release title, release notes, support links, security links, and consumer example.
- Publish the release/listing.
- Open the resulting Marketplace page in a logged-out browser and verify installation instructions and links.
- Only after that verification, replace the "Marketplace forthcoming" notice in the READMEs and consumer guides with the real listing URL.

## Verification / badges

A Marketplace **verified creator** badge is separate from ordinary Action publication. GitHub documents it as a partner-organization badge; it is not a prerequisite for publishing this Action.

Do not add a Marketplace badge or claim Marketplace availability until the listing has actually been published and its URL has been verified.

## Post-publication checks

After publication:

1. verify the Marketplace page shows the expected name, description, author, shield/purple branding, categories, and release version;
2. validate a clean consumer workflow using the published `@v1`;
3. confirm the Action still resolves to the expected GHCR major image;
4. verify support, security, license, release notes, and bilingual documentation links;
5. update [github-action.md](github-action.md), [github-action.pt-BR.md](github-action.pt-BR.md), `README.md`, and `README.pt-BR.md` with the verified Marketplace URL.
