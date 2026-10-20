# Packaging and downloading ADR Guard for VS Code

The VS Code extension is versioned independently from the ADR Guard CLI and GitHub Action. The extension identifier is `rodrioliveira.adr-guard`, and the VSIX version is read from `extensions/vscode/package.json`.

## Build a validated VSIX (no Marketplace publication)

1. Confirm the required ADR Guard CLI capabilities are available for the extension version being tested. The VSIX does not bundle or install the CLI.
2. Open [GitHub Actions — VS Code VSIX Package](https://github.com/rodri-oliveira-dev/adr-guard/actions/workflows/vscode-extension-release.yml).
3. Select **Run workflow**, choose the `main` branch, and start the run. No version input or publishing confirmation is required: the version is taken from the extension manifest.
4. The workflow installs dependencies, audits, lints, typechecks, builds, and runs unit, localization, extension-host integration, README image, and VSIX installation/activation tests.
5. When the run succeeds, open its **Summary → Artifacts** section and download `adr-guard-vscode-<version>` (a ZIP archive containing `adr-guard-<version>.vsix`). The package is retained for **21 days** from the workflow run, subject to the repository's maximum artifact retention setting.
6. Extract the ZIP and install the `.vsix` in VS Code using **Extensions: Install from VSIX...**, or run `code --install-extension adr-guard-<version>.vsix`.

The [VS Code Extension CI](https://github.com/rodri-oliveira-dev/adr-guard/actions/workflows/vscode-extension.yml) also uploads a VSIX artifact with a version derived from the manifest and a 21-day retention period whenever its checks run. For the most recently packaged version, select the latest **successful** run for the intended branch and verify the version displayed in the artifact name.

## Distribution and safety controls

- The dedicated packaging workflow is **manual only** and its packaging job executes only on `main`. Pull requests do not trigger this workflow.
- The workflow **does not publish to the Visual Studio Marketplace**, request an OIDC publishing token, or create a deployment to the `vscode-marketplace` environment. A Marketplace listing published previously is unaffected.
- No publishing credentials, protected environment approval, or Marketplace trusted-publisher configuration is needed to create and download the VSIX.
- Every successful workflow run stores its own artifact for 21 days. Artifacts from earlier runs expire independently; GitHub Actions does not automatically delete earlier artifacts when a newer version is packaged.
- Download files from the workflow run's **Artifacts** section, not from the historic [Marketplace deployments](https://github.com/rodri-oliveira-dev/adr-guard/deployments/vscode-marketplace) page.
- The extension version must be stable `MAJOR.MINOR.PATCH` and the manifest must identify publisher `rodrioliveira` and package `adr-guard`.

Version 1.1.0 is already available in the Marketplace, but this repository workflow did not publish it and has no Marketplace credentials. Any future Marketplace update still requires a **separate, explicitly approved publication path** and Marketplace authentication. VSIX packaging alone never updates the Marketplace.
