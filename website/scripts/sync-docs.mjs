import { mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { dirname, posix, resolve, relative, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const websiteRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const repositoryRoot = resolve(websiteRoot, '..');
const contentRoot = resolve(websiteRoot, 'src/content/docs');

const concepts = [
  ['docs/fundamentals/what-is-an-adr.md', 'learn/what-is-an-adr.md'],
  ['docs/fundamentals/why-use-adrs.md', 'learn/why-use-adrs.md'],
  ['docs/fundamentals/when-to-write-an-adr.md', 'learn/when-to-write-an-adr.md'],
  ['docs/fundamentals/lifecycle.md', 'learn/lifecycle.md'],
  ['docs/decision-design/decision-categories.md', 'learn/decision-categories.md'],
  ['docs/decision-design/formats-and-templates.md', 'learn/formats-and-templates.md'],
  ['docs/decision-design/choosing-a-template.md', 'learn/choosing-a-template.md'],
  ['docs/decision-design/writing-effective-adrs.md', 'learn/writing-effective-adrs.md'],
  ['docs/decision-design/anti-patterns.md', 'learn/anti-patterns.md'],
  ['docs/fundamentals/glossary.md', 'learn/glossary.md']
];

const product = [
  ['docs/cli-reference.md', 'product/cli.md'],
  ['docs/creation.md', 'product/creation.md'],
  ['docs/custom-templates.md', 'product/custom-templates.md'],
  ['docs/madr-4.md', 'product/madr-4.md'],
  ['docs/github-action.md', 'product/github-action.md'],
  ['extensions/vscode/README.md', 'product/vscode-extension.md'],
  ['docs/container.md', 'product/containers.md'],
  ['docs/draft-templates.md', 'product/ai-drafting.md'],
  ['docs/adr-review.md', 'product/ai-review.md'],
  ['docs/comparative-review.md', 'product/comparative-review.md'],
  ['docs/relationship-governance.md', 'product/relationships.md'],
  ['docs/incremental-validation.md', 'product/incremental-validation.md'],
  ['docs/check-reports.md', 'product/reports.md'],
  ['docs/adr-review-security.md', 'product/security.md'],
  ['docs/guides/team-adoption.md', 'adoption/team-playbook.md']
];

const references = [
  ['docs/adr-review-policy-v1.md', 'reference/generated/review-policy-v1.md'],
  ['docs/github-action-security.md', 'reference/generated/github-action-security.md'],
  ['docs/github-action-review.md', 'reference/generated/github-action-review.md'],
  ['docs/github-action-release.md', 'reference/generated/github-action-release.md'],
  ['docs/github-action-external-verification.md', 'reference/generated/github-action-external-verification.md']
];

const localized = (entries) => entries.map(([source, destination]) => {
  const extensionReadme = source === 'extensions/vscode/README.md';
  const ptSource = extensionReadme ? 'extensions/vscode/README.pt-BR.md' : source.replace(/\.md$/, '.pt-BR.md');
  return [ptSource, `pt-br/${destination}`];
});

const mappings = [...concepts, ...product, ...references, ...localized(concepts), ...localized(product), ...localized(references)];
const sourceToRoute = new Map(mappings.map(([source, destination]) => [source.toLowerCase(), destination.replace(/\.md$/, '/') ]));

function normalizeRepositoryPath(path) {
  return relative(repositoryRoot, path).split(sep).join('/');
}

function quoteYaml(value) {
  return `'${value.replaceAll("'", "''")}'`;
}

function titleFromMarkdown(markdown, fallback) {
  return markdown.match(/^#\s+(.+)$/m)?.[1]?.replace(/[*`]/g, '').trim() ?? fallback;
}

function descriptionFromMarkdown(markdown, fallback) {
  const withoutFrontmatter = markdown.replace(/^---\r?\n[\s\S]*?\r?\n---\r?\n/, '');
  const paragraphs = withoutFrontmatter
    .replace(/^#\s+.+\r?\n/, '')
    .split(/\r?\n\s*\r?\n/)
    .map((paragraph) => paragraph.trim())
    .filter((paragraph) => paragraph && !/^(?:#|```|:::|>|-|\*|\d+\.)/.test(paragraph))
    .filter((paragraph) => !/^\[(?:Português|English|Versão em português|Read in English)\b/i.test(paragraph));
  const description = paragraphs[0]
    ?.replace(/\[([^\]]+)\]\([^)]+\)/g, '$1')
    .replace(/<[^>]+>/g, '')
    .replace(/[*_`]/g, '')
    .replace(/\s+/g, ' ')
    .trim();
  if (!description) return fallback;
  return description.length <= 170 ? description : `${description.slice(0, 167).replace(/\s+\S*$/, '')}…`;
}

function rewriteMarkdown(markdown, sourcePath, destination, title) {
  let body = markdown.replace(/^---\r?\n[\s\S]*?\r?\n---\r?\n/, '');
  body = body.replace(/^#\s+.+\r?\n/, '');
  body = body.replace(/^\[(?:Português|English|Versão em português|Read in English)[^\n]*\]\([^\n]+\)\s*\r?\n+/i, '');
  body = body.replace(/\[([^\]]+)\]\(([^)]+\.md(?:#[^)]+)?)\)/g, (match, label, target) => {
    if (/^(?:https?:|mailto:)/.test(target)) return match;
    const [filePart, hash = ''] = target.split('#');
    const absolute = resolve(dirname(sourcePath), filePart);
    const repositoryPath = normalizeRepositoryPath(absolute).toLowerCase();
    const route = sourceToRoute.get(repositoryPath);
    if (route) return `[${label}](/adr-guard/${route}${hash ? `#${hash}` : ''})`;
    const githubPath = normalizeRepositoryPath(absolute);
    return `[${label}](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/${githubPath}${hash ? `#${hash}` : ''})`;
  });
  body = body.replace(/\[([^\]]+)\]\((?!https?:|mailto:|#|\/)([^)]+)\)/g, (match, label, target) => {
    const [filePart, hash = ''] = target.split('#');
    const githubPath = normalizeRepositoryPath(resolve(dirname(sourcePath), filePart));
    return `[${label}](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/${githubPath}${hash ? `#${hash}` : ''})`;
  });
  const sourceRelative = normalizeRepositoryPath(sourcePath);
  const editUrl = `https://github.com/rodri-oliveira-dev/adr-guard/edit/main/${sourceRelative}`;
  const description = descriptionFromMarkdown(markdown, `${title} documentation for ADR Guard.`);
  return `---\neditUrl: ${quoteYaml(editUrl)}\ntitle: ${quoteYaml(title)}\ndescription: ${quoteYaml(description)}\nsidebar:\n  badge:\n    text: Source synced\n    variant: note\n---\n\n<!-- Generated by scripts/sync-docs.mjs from ${sourceRelative}. Do not edit this file. -->\n\n${body.trim()}\n`;
}

async function main() {
  await rm(resolve(contentRoot, 'learn'), { recursive: true, force: true });
  await rm(resolve(contentRoot, 'pt-br/learn'), { recursive: true, force: true });
  await rm(resolve(contentRoot, 'reference/generated'), { recursive: true, force: true });
  await rm(resolve(contentRoot, 'pt-br/reference/generated'), { recursive: true, force: true });

  for (const [source, destination] of mappings) {
    const sourcePath = resolve(repositoryRoot, source);
    const destinationPath = resolve(contentRoot, destination);
    const markdown = await readFile(sourcePath, 'utf8');
    const title = titleFromMarkdown(markdown, posix.basename(destination, '.md'));
    await mkdir(dirname(destinationPath), { recursive: true });
    await writeFile(destinationPath, rewriteMarkdown(markdown, sourcePath, destination, title), 'utf8');
  }
  console.log(`Synchronized ${mappings.length} source documents.`);
}

await main();
