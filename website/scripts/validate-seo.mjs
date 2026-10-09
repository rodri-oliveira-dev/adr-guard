import { readFile, readdir, stat } from 'node:fs/promises';
import { relative, resolve, sep } from 'node:path';

const dist = resolve(import.meta.dirname, '../dist');
const site = 'https://rodri-oliveira-dev.github.io';
const basePath = '/adr-guard/';
const failures = [];
const pages = [];

async function walk(directory) {
  for (const name of await readdir(directory)) {
    const path = resolve(directory, name);
    if ((await stat(path)).isDirectory()) await walk(path);
    else if (name.endsWith('.html')) pages.push(path);
  }
}

function attributes(source) {
  return Object.fromEntries([...source.matchAll(/([:\w-]+)="([^"]*)"/g)].map((match) => [match[1], match[2]]));
}

function tags(html, name) {
  return [...html.matchAll(new RegExp(`<${name}\\b([^>]*)>`, 'gi'))].map((match) => attributes(match[1]));
}

function meta(html, key, value) {
  return tags(html, 'meta').filter((entry) => entry[key] === value);
}

function links(html, rel) {
  return tags(html, 'link').filter((entry) => entry.rel === rel);
}

function routeFor(file) {
  const path = relative(dist, file).split(sep).join('/');
  if (path === 'index.html') return basePath;
  if (path === '404.html') return `${basePath}404/`;
  if (path.endsWith('/index.html')) return `${basePath}${path.slice(0, -'index.html'.length)}`;
  return `${basePath}${path}`;
}

function fail(file, message) {
  failures.push(`${relative(dist, file).split(sep).join('/')}: ${message}`);
}

await walk(dist);

const indexable = new Map();
const titleOwners = new Map();
const descriptionOwners = new Map();

for (const file of pages) {
  const html = await readFile(file, 'utf8');
  const route = routeFor(file);
  const expectedUrl = `${site}${route}`;
  const isPortuguese = route.startsWith(`${basePath}pt-br/`);
  const isNotFound = /\/404(?:\.html|\/)$/.test(route);
  const lang = html.match(/<html\b[^>]*\blang="([^"]+)"/i)?.[1];
  const title = html.match(/<title>([\s\S]*?)<\/title>/i)?.[1]?.trim();
  const descriptions = meta(html, 'name', 'description');
  const canonicals = links(html, 'canonical');
  const robots = meta(html, 'name', 'robots');
  const alternates = links(html, 'alternate').filter((entry) => entry.hreflang);

  if (lang !== (isPortuguese ? 'pt-BR' : 'en')) fail(file, `expected html lang ${isPortuguese ? 'pt-BR' : 'en'}, found ${lang ?? 'none'}`);
  if (!title) fail(file, 'missing title');
  if (descriptions.length !== 1 || !descriptions[0].content) fail(file, `expected one non-empty meta description, found ${descriptions.length}`);
  if (canonicals.length !== 1) fail(file, `expected one canonical, found ${canonicals.length}`);
  else if (canonicals[0].href !== expectedUrl) fail(file, `canonical ${canonicals[0].href} does not match ${expectedUrl}`);
  if (robots.length !== 1) fail(file, `expected one robots meta, found ${robots.length}`);
  if (isNotFound && !robots[0]?.content?.includes('noindex')) fail(file, '404 must be noindex');

  const requiredMeta = [
    ['property', 'og:title'], ['property', 'og:description'], ['property', 'og:url'], ['property', 'og:image'],
    ['property', 'og:image:width'], ['property', 'og:image:height'], ['property', 'og:image:alt'],
    ['name', 'twitter:card'], ['name', 'twitter:title'], ['name', 'twitter:description'], ['name', 'twitter:image'], ['name', 'twitter:image:alt']
  ];
  for (const [key, value] of requiredMeta) {
    const matches = meta(html, key, value);
    if (matches.length !== 1 || !matches[0].content) fail(file, `expected one non-empty ${value}, found ${matches.length}`);
  }

  for (const entry of [...tags(html, 'a'), ...tags(html, 'link'), ...tags(html, 'script'), ...tags(html, 'img')]) {
    const target = entry.href ?? entry.src;
    if (target?.startsWith('/') && !target.startsWith(basePath)) fail(file, `root-relative URL escapes base path: ${target}`);
  }

  if (!isNotFound) {
    indexable.set(expectedUrl, { file, html, alternates });
    const hreflang = Object.fromEntries(alternates.map((entry) => [entry.hreflang, entry.href]));
    const englishUrl = isPortuguese ? expectedUrl.replace(`${basePath}pt-br/`, basePath) : expectedUrl;
    const portugueseUrl = isPortuguese ? expectedUrl : expectedUrl.replace(basePath, `${basePath}pt-br/`);
    if (alternates.length !== 3) fail(file, `expected three hreflang links, found ${alternates.length}`);
    if (hreflang.en !== englishUrl || hreflang['pt-BR'] !== portugueseUrl || hreflang['x-default'] !== englishUrl) {
      fail(file, 'hreflang cluster is incomplete or points to the wrong localized URL');
    }

    const scripts = [...html.matchAll(/<script\b[^>]*type="application\/ld\+json"[^>]*>([\s\S]*?)<\/script>/gi)];
    if (scripts.length !== 1) fail(file, `expected one JSON-LD block, found ${scripts.length}`);
    else {
      try {
        const graph = JSON.parse(scripts[0][1])['@graph'];
        const types = new Set(graph.flatMap((item) => Array.isArray(item['@type']) ? item['@type'] : [item['@type']]));
        for (const type of ['WebSite', 'SoftwareApplication']) if (!types.has(type)) fail(file, `JSON-LD missing ${type}`);
        if (route !== basePath && route !== `${basePath}pt-br/`) {
          for (const type of ['TechArticle', 'BreadcrumbList']) if (!types.has(type)) fail(file, `JSON-LD missing ${type}`);
        }
      } catch (error) {
        fail(file, `invalid JSON-LD: ${error.message}`);
      }
    }

    if (title) {
      const owners = titleOwners.get(title) ?? [];
      owners.push(file);
      titleOwners.set(title, owners);
    }
    if (descriptions[0]?.content) {
      const owners = descriptionOwners.get(descriptions[0].content) ?? [];
      owners.push(file);
      descriptionOwners.set(descriptions[0].content, owners);
    }
  }
}

for (const [url, page] of indexable) {
  for (const alternate of page.alternates) {
    if (!indexable.has(alternate.href)) fail(page.file, `hreflang target is not an indexable built page: ${alternate.href}`);
  }
}

for (const [value, owners] of [...titleOwners, ...descriptionOwners]) {
  if (owners.length > 1) failures.push(`duplicate metadata on ${owners.map((file) => relative(dist, file)).join(', ')}: ${value}`);
}

const sitemapIndex = await readFile(resolve(dist, 'sitemap-index.xml'), 'utf8');
const sitemapFiles = [...sitemapIndex.matchAll(/<loc>[^<]*\/(sitemap-[^<]+\.xml)<\/loc>/g)].map((match) => match[1]);
if (sitemapFiles.length === 0) failures.push('sitemap-index.xml: no child sitemaps');
const sitemapUrls = new Set();
for (const sitemapFile of sitemapFiles) {
  const xml = await readFile(resolve(dist, sitemapFile), 'utf8');
  for (const block of xml.matchAll(/<url>([\s\S]*?)<\/url>/g)) {
    const location = block[1].match(/<loc>([^<]+)<\/loc>/)?.[1];
    if (!location) continue;
    sitemapUrls.add(location);
    if (!location.startsWith(`${site}${basePath}`)) failures.push(`${sitemapFile}: URL outside portal base path: ${location}`);
    const alternateCount = [...block[1].matchAll(/<xhtml:link\b/g)].length;
    if (alternateCount !== 2) failures.push(`${sitemapFile}: ${location} has ${alternateCount} language alternates; expected 2`);
  }
}

for (const url of indexable.keys()) if (!sitemapUrls.has(url)) failures.push(`sitemap: missing indexable canonical ${url}`);
for (const url of sitemapUrls) if (!indexable.has(url)) failures.push(`sitemap: contains non-indexable or missing page ${url}`);

if (failures.length) throw new Error(`SEO validation failed (${failures.length}):\n${failures.join('\n')}`);
console.log(`Validated SEO metadata, JSON-LD, hreflang, and sitemap for ${indexable.size} indexable pages.`);
