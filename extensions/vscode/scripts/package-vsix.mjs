import { spawnSync } from 'node:child_process';
import { readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { createRequire } from 'node:module';

const root = path.resolve(import.meta.dirname, '..');
const require = createRequire(import.meta.url);
const vsceCli = require.resolve('@vscode/vsce/vsce');
const manifest = JSON.parse(await readFile(path.join(root, 'package.json'), 'utf8'));
const documents = ['README.md', 'README.pt-BR.md', 'CHANGELOG.md'];
const originals = new Map();

function replaceExactlyOnce(content, pattern, replacement, description) {
  const matches = content.match(pattern);
  if (matches?.length !== 1) {
    throw new Error(`Expected exactly one ${description}; found ${matches?.length ?? 0}.`);
  }

  return content.replace(pattern, replacement);
}

function prepareReadme(content, locale) {
  const escapedVersion = manifest.version.replaceAll('.', '\\.');
  const packageStatus = locale === 'pt-BR'
    ? `> **Versão deste pacote:** este VSIX contém a extensão ADR Guard ${manifest.version}. A extensão, a CLI e o MCP são instalados e versionados separadamente; consulte o Marketplace e os registros correspondentes para confirmar a disponibilidade pública.`
    : `> **Package version:** this VSIX contains ADR Guard extension ${manifest.version}. The extension, CLI, and MCP are installed and versioned separately; check the Marketplace and the corresponding registries for current public availability.`;
  const compatibility = locale === 'pt-BR'
    ? `| Extensão ${manifest.version} | Versão deste pacote; confirme a disponibilidade pública no Visual Studio Marketplace. |`
    : `| Extension ${manifest.version} | This package version; verify public availability in the Visual Studio Marketplace. |`;
  const compatibilityPattern = locale === 'pt-BR'
    ? new RegExp(`^\\| Extensão ${escapedVersion} \\| Candidata de manutenção preparada; ainda não publicada\\. \\|$`, 'gm')
    : new RegExp(`^\\| Extension ${escapedVersion} \\| Prepared maintenance candidate; not yet published\\. \\|$`, 'gm');

  let prepared = replaceExactlyOnce(
    content,
    /<!-- vsix-package-status:start -->[\s\S]*?<!-- vsix-package-status:end -->/g,
    packageStatus,
    'VSIX package status block',
  );
  prepared = replaceExactlyOnce(
    prepared,
    compatibilityPattern,
    compatibility,
    'VSIX package compatibility row',
  );
  return prepared;
}

function prepareChangelog(content) {
  const escapedVersion = manifest.version.replaceAll('.', '\\.');
  return replaceExactlyOnce(
    content,
    new RegExp(`^## ${escapedVersion} — prepared (\\d{4}-\\d{2}-\\d{2}), not yet published$`, 'gm'),
    `## ${manifest.version} — $1`,
    `prepared ${manifest.version} changelog heading`,
  );
}

let packageResult;
try {
  for (const document of documents) {
    const documentPath = path.join(root, document);
    const content = await readFile(documentPath, 'utf8');
    originals.set(documentPath, content);
    const prepared = document === 'CHANGELOG.md'
      ? prepareChangelog(content)
      : prepareReadme(content, document.includes('pt-BR') ? 'pt-BR' : 'en');
    await writeFile(documentPath, prepared, 'utf8');
  }

  packageResult = spawnSync(
    process.execPath,
    [vsceCli, 'package', '--no-dependencies', ...process.argv.slice(2)],
    { cwd: root, stdio: 'inherit' },
  );
} finally {
  await Promise.all(
    [...originals].map(([documentPath, content]) => writeFile(documentPath, content, 'utf8')),
  );
}

if (packageResult.error) throw packageResult.error;
if (packageResult.signal) throw new Error(`VSIX packaging terminated by ${packageResult.signal}.`);
if (packageResult.status !== 0) process.exitCode = packageResult.status ?? 1;
