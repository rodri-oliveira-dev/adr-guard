import { spawnSync } from 'node:child_process';
import { readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { createRequire } from 'node:module';

const defaultRoot = path.resolve(import.meta.dirname, '..');
const require = createRequire(import.meta.url);

function replaceExactlyOnce(content, pattern, replacement, description) {
  const matches = content.match(pattern);
  if (matches?.length !== 1) {
    throw new Error(`Expected exactly one ${description}; found ${matches?.length ?? 0}.`);
  }

  return content.replace(pattern, replacement);
}

export function prepareReadme(content, locale, version) {
  const escapedVersion = version.replaceAll('.', '\\.');
  const packageStatus = locale === 'pt-BR'
    ? `> **Versão deste pacote:** este VSIX contém a extensão ADR Guard ${version}. A extensão, a CLI e o MCP são instalados e versionados separadamente; consulte o Marketplace e os registros correspondentes para confirmar a disponibilidade pública.`
    : `> **Package version:** this VSIX contains ADR Guard extension ${version}. The extension, CLI, and MCP are installed and versioned separately; check the Marketplace and the corresponding registries for current public availability.`;
  const compatibility = locale === 'pt-BR'
    ? `| Extensão ${version} | Versão deste pacote; confirme a disponibilidade pública no Visual Studio Marketplace. |`
    : `| Extension ${version} | This package version; verify public availability in the Visual Studio Marketplace. |`;
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

export function prepareChangelog(content, version) {
  const escapedVersion = version.replaceAll('.', '\\.');
  return replaceExactlyOnce(
    content,
    new RegExp(`^## ${escapedVersion} — prepared (\\d{4}-\\d{2}-\\d{2}), not yet published$`, 'gm'),
    `## ${version} — $1`,
    `prepared ${version} changelog heading`,
  );
}

function executeVsce({ root, packageArguments }) {
  const vsceCli = require.resolve('@vscode/vsce/vsce');
  return spawnSync(
    process.execPath,
    [vsceCli, 'package', '--no-dependencies', ...packageArguments],
    { cwd: root, stdio: 'inherit' },
  );
}

export async function packageVsix({
  root = defaultRoot,
  packageArguments = process.argv.slice(2),
  executePackage = executeVsce,
} = {}) {
  const manifest = JSON.parse(await readFile(path.join(root, 'package.json'), 'utf8'));
  const documents = ['README.md', 'README.pt-BR.md', 'CHANGELOG.md'];
  const originals = new Map();
  let packageResult;

  try {
    for (const document of documents) {
      const documentPath = path.join(root, document);
      const content = await readFile(documentPath, 'utf8');
      originals.set(documentPath, content);
      const prepared = document === 'CHANGELOG.md'
        ? prepareChangelog(content, manifest.version)
        : prepareReadme(content, document.includes('pt-BR') ? 'pt-BR' : 'en', manifest.version);
      await writeFile(documentPath, prepared, 'utf8');
    }

    packageResult = executePackage({ root, packageArguments });
  } finally {
    await Promise.all(
      [...originals].map(([documentPath, content]) => writeFile(documentPath, content, 'utf8')),
    );
  }

  if (packageResult.error) throw packageResult.error;
  if (packageResult.signal) throw new Error(`VSIX packaging terminated by ${packageResult.signal}.`);
  return packageResult.status ?? 1;
}

const isMain = process.argv[1] !== undefined
  && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href;
if (isMain) process.exitCode = await packageVsix();
