# Publishing ADR Guard for VS Code

The VS Code extension is versioned independently from the ADR Guard CLI and GitHub Action. The Marketplace extension identifier is `rodrioliveira.adr-guard`.

## One-time owner prerequisites

1. In the Visual Studio Marketplace publisher management portal, configure a trusted publishing policy for publisher `rodrioliveira`, repository `rodri-oliveira-dev/adr-guard` and workflow `.github/workflows/vscode-extension-release.yml`. Follow the Marketplace's current trusted-publisher setup instructions.
2. Create a protected GitHub Actions environment named `vscode-marketplace`. Add required reviewers and, if appropriate, restrict deployment to the `main` branch. Merely referencing an environment in a workflow does **not** configure reviewer protection.
3. Ensure a compatible ADR Guard .NET CLI version is publicly installable and its advanced contracts are available. The VSIX does not include the CLI or install it automatically.
4. Review the English and Portuguese documentation and the changelog. This workflow preparation intentionally does not introduce an extension icon.

## First release

1. Confirm the combined CLI capabilities (previous v1.3 and v1.4 development scopes) have been released in NuGet v1.3.0; the VS Code extension and release automation are already integrated into `main`.
2. Check `extensions/vscode/package.json` for publisher `rodrioliveira`, package name `adr-guard` and version `0.1.0`. Keep the VSIX output filename and smoke test aligned with the chosen version for future releases.
3. Open GitHub Actions and select **VS Code Marketplace Release**. Choose the `main` branch, specify version `0.1.0` and enter exactly `publish-rodrioliveira.adr-guard` as confirmation.
4. The package job verifies the manifest and reruns dependency auditing, lint, typecheck, build, unit, localization and VS Code host integration tests. It then creates and tests the VSIX.
5. After the protected `vscode-marketplace` environment approval, the separate publishing job downloads that validated VSIX and runs `vsce publish --oidc --packagePath`. No personal access tokens or GitHub repository secrets are needed for Marketplace authentication.
6. Verify the listing at https://marketplace.visualstudio.com/items?itemName=rodrioliveira.adr-guard and independently test installation from VS Code.

## Release controls

- The release workflow is **manual only**, does not publish on a PR, push, or tag, and only executes on `main` with an exact confirmation string.
- Restricting the publishing job to a reviewed GitHub environment is a recommended owner-side configuration. Until it is protected, manually invoking the workflow can still reach the publish job if trusted publishing is configured.
- The OIDC token permission is scoped to the publishing job, not the build job.
- Marketplace trusted publishing must be configured before attempting the first release. An OIDC error is a configuration failure; do not work around it by committing a long-lived PAT.
- If publishing fails, review logs and re-run the manual workflow after fixing the problem. Do not silently retry or overwrite a public version.

References: https://github.com/microsoft/vscode-vsce#trusted-publishing and https://code.visualstudio.com/api/working-with-extensions/publishing-extension
