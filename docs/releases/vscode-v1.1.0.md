# ADR Guard for VS Code 1.1.0 — Source candidate (not published)

[Português (Brasil)](vscode-v1.1.0.pt-BR.md)

The independently versioned **ADR Guard for VS Code 1.1.0** candidate is available in source control, but **has not been published as a 1.1.0 update on the Visual Studio Marketplace**. The already published Marketplace version, if any, is independent from this candidate. A successful CI run or downloadable VSIX artifact does not imply Marketplace publication.

## What's in this candidate

- A visual workflow for ADR browsing, creation, editing, settings, validation, architecture impact and drift, baselines, enriched catalogs, and optional AI-assisted drafting/review.
- An optional MCP registration provider, disabled by default, that requires a compatible, independently installed MCP server.
- Bilingual UI and explicit execution, permission, and workspace trust boundaries.

For individual capabilities and CLI requirements, see the [extension README](../../extensions/vscode/README.md) and [compatibility matrix](../compatibility.md). CLI 1.8.0 is the recommended companion for the full visual workflow; MCP 1.0.0 is independent and must not be assumed to be published.

## Download a validated VSIX (21-day window)

1. After the packaging workflow changes have merged into `main`, open [Actions — VS Code VSIX Package](https://github.com/rodri-oliveira-dev/adr-guard/actions/workflows/vscode-extension-release.yml).
2. Select **Run workflow** on `main`. The workflow reads the extension version from `extensions/vscode/package.json`; it does not take publishing confirmation or Marketplace credentials.
3. Wait for successful checks and packaging, then open the run's **Summary → Artifacts**.
4. Download `adr-guard-vscode-1.1.0` (or the manifest-derived version displayed on a newer run), extract the ZIP, and find `adr-guard-1.1.0.vsix` (or the matching version).
5. Use **Extensions: Install from VSIX...** in VS Code, or `code --install-extension adr-guard-1.1.0.vsix` from the extracted directory.

The artifact retention setting is **21 days per successful run**, subject to the repository's maximum allowed retention. Older run artifacts expire independently; the most recently packaged candidate is identified by the **latest successful run and its artifact version**, not by the historical deployments page. The [VS Code Extension CI](https://github.com/rodri-oliveira-dev/adr-guard/actions/workflows/vscode-extension.yml) produces the same versioned artifact on its applicable runs with 21-day retention.

## Marketplace publication status

The packaging and CI workflows **never call `vsce publish`**, request Marketplace publishing credentials, or deploy to the `vscode-marketplace` environment. To distribute a new Marketplace version, the project would need to introduce a **separate, explicitly authorized publishing workflow**. Previously published Marketplace versions remain unaffected by packaging changes.

See the [packaging guide](../../extensions/vscode/PUBLISHING.md) for the complete procedure.
