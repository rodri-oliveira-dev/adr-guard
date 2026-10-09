# SEO and discoverability maintenance

The ADR Guard portal is a static Astro + Starlight site published at `https://rodri-oliveira-dev.github.io/adr-guard/`. English is the default locale and Brazilian Portuguese is served under `/pt-br/`.

## Architecture

- Starlight owns titles, descriptions, canonical URLs, HTML language, and localized alternate links.
- `src/routeData.ts` adds robots directives, Open Graph/Twitter metadata, and one JSON-LD graph per indexable page.
- `@astrojs/sitemap` discovers routes at build time, excludes 404 pages, and emits EN/pt-BR alternates. Do not maintain a manual URL list or synthetic `lastmod` dates.
- `scripts/validate-seo.mjs` validates the built HTML and sitemap. It checks uniqueness, canonical/base-path consistency, reciprocal translations, social metadata, JSON-LD, and sitemap membership.
- `public/social-preview-v2.png` is the 1200×630 social image. Its public URL and declared dimensions must stay synchronized with `src/routeData.ts`.

## Local quality gates

From `website/`:

```bash
npm ci
npm run validate
npm run test:spelling
```

`validate` performs type/content checks, component tests, a production build, route/link checks, SEO validation, and generated HTML validation. Lighthouse and Pa11y require a browser and run in dedicated workflows against `astro preview` with the `/adr-guard/` base path.

## Adding or translating a page

1. Add the English page and a complete counterpart at the same route under `src/content/docs/pt-br/`.
2. Write a unique, natural title and description in frontmatter. Keep the visible H1 and page purpose aligned.
3. Link contextually to relevant foundations, examples, adoption guidance, and product reference pages.
4. Run the production build and all local gates. Confirm that the route appears once in the sitemap and has reciprocal `en`, `pt-BR`, and `x-default` links.
5. For source-synchronized pages, edit the authoritative repository document and improve `scripts/sync-docs.mjs`; never edit ignored generated output.

### New-page checklist

- [ ] Unique localized title and description
- [ ] Correct content language and equivalent translated route
- [ ] Self-referencing canonical
- [ ] Reciprocal hreflang and English `x-default`
- [ ] Complete Open Graph and Twitter image metadata
- [ ] Relevant, accurate structured data
- [ ] Inclusion in the generated sitemap (unless intentionally `noindex`)
- [ ] Contextual internal links and valid anchors
- [ ] Logical heading hierarchy and accessible interactive controls
- [ ] Production build, SEO validation, HTML validation, and spelling checks pass

## Search engine operations

The portal can be publicly deployed and crawlable without being discovered or indexed. These are separate states:

1. **Published:** GitHub Pages serves the production build.
2. **Crawlable:** robots directives, status codes, links, and canonical metadata allow crawling.
3. **Sitemap submitted:** a verified Search Console or Bing Webmaster Tools property has received `https://rodri-oliveira-dev.github.io/adr-guard/sitemap-index.xml`.
4. **Discovered:** the service reports that it knows the URL.
5. **Indexed:** URL Inspection or the indexing reports explicitly confirm inclusion.

Because the portal shares `rodri-oliveira-dev.github.io` with the portfolio, domain/URL-prefix ownership and the effective root `robots.txt` belong to the host owner. Verify an appropriate property in Google Search Console and Bing Webmaster Tools, submit the sitemap, inspect representative EN and pt-BR URLs, and monitor excluded pages, crawl errors, selected canonicals, and localized variants. Do not claim indexing until those services provide evidence.

## External dependencies and limitations

- The host-root portfolio should advertise the ADR Guard sitemap from its root `robots.txt`; this repository cannot make `/adr-guard/robots.txt` authoritative for the host.
- IndexNow is not enabled here. Its ownership key must be publicly verifiable at the host-required location, so reusing the portfolio key or claiming operation without host-owner verification would be unsafe.
- Automated accessibility checks cover detectable WCAG issues, including axe rules, but do not replace keyboard, screen-reader, zoom, language-switcher, or cognitive-accessibility review.
- External-link checks exclude LinkedIn and Medium because those hosts block automated clients. On pull requests only, they also exclude this repository's `main` file URLs because newly added files do not exist there until merge; scheduled and manual runs include those URLs.
- Google rich results are not guaranteed. Validate deployed structured data with the Rich Results Test and monitor Search Console enhancement reports.
