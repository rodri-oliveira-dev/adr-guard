# ADR Guard documentation portal

The portal is a static Astro + Starlight site published under `/adr-guard/`. English is served at the root and Brazilian Portuguese under `/pt-br/`.

## Requirements

- Node.js 24 or later
- npm (the committed lockfile is authoritative)

## Local development

```bash
cd website
npm ci
npm run dev
```

The pre-development hook synchronizes selected repository documentation before Astro starts. Use the local URL including the configured base path: `http://localhost:4321/adr-guard/`.

## Validation and production build

```bash
npm run check
npm test
npm run build
npm run test:routes
npm run test:links
```

Run all gates with `npm run validate`. Search is provided by Starlight’s Pagefind integration and must be tested after `npm run build`, not only in development mode.

## Content ownership

There are two content classes:

1. **Portal-owned content** lives in `website/src/content/docs/` and includes landing pages, learning journeys, adoption material, and explanatory example pages.
2. **Source-synchronized product and concept documentation** remains authoritative under the repository’s `docs/` tree and `extensions/vscode/README*.md`. `scripts/sync-docs.mjs` has an explicit source-to-route map, adds deterministic frontmatter, rewrites mapped relative Markdown links, and generates ignored files before development, checks, and builds.

Never edit a generated page under `learn/`, a generated `product/*.md` page, or `reference/generated/`. Edit its source document and run `npm run sync:docs`. The generated output is intentionally not committed.

## Add or translate a page

- Add an English portal page under `src/content/docs/<route>.md(x)`.
- Add its complete Brazilian Portuguese counterpart under `src/content/docs/pt-br/<route>.md(x)`.
- Keep both filenames and headings structurally aligned so Starlight can preserve the equivalent route in the language picker.
- Add a source document to the explicit mapping in `scripts/sync-docs.mjs` only when the source has both language versions or an intentionally documented exception.

## Add an example

Keep validator-compatible source ADRs in `docs/examples/`, where repository tests can validate them. Add the educational scenario page to both portal locales and link to the source files. Do not claim compatibility for illustrative Markdown that does not follow a supported contract.

## Publish

`.github/workflows/documentation-portal.yml` validates pull requests that affect the portal and deploys only after a matching push reaches `main` (or a manual run). In repository **Settings → Pages**, set the source to **GitHub Actions** once. The planned URL is:

`https://rodri-oliveira-dev.github.io/adr-guard/`

Do not report the portal as published until the Pages deployment job succeeds and returns that URL.
