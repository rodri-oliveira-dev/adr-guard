import assert from 'node:assert/strict';
import { mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import {
  packageVsix,
  prepareChangelog,
  prepareReadme,
} from '../../scripts/package-vsix.mjs';

const version = '1.1.1';
const englishReadme = `# Extension
<!-- vsix-package-status:start -->
> Version ${version} is not yet published.
<!-- vsix-package-status:end -->
| Extension ${version} | Prepared maintenance candidate; not yet published. |
`;
const portugueseReadme = `# Extensão
<!-- vsix-package-status:start -->
> A versão ${version} ainda não foi publicada.
<!-- vsix-package-status:end -->
| Extensão ${version} | Candidata de manutenção preparada; ainda não publicada. |
`;
const changelog = `# Changelog

## ${version} — prepared 2026-11-05, not yet published
`;

test('prepares bilingual package metadata without claiming Marketplace publication', () => {
  const english = prepareReadme(englishReadme, 'en', version);
  const portuguese = prepareReadme(portugueseReadme, 'pt-BR', version);

  assert.match(english, /\*\*Package version:\*\*.*1\.1\.1/);
  assert.match(english, /verify public availability in the Visual Studio Marketplace/);
  assert.match(portuguese, /\*\*Versão deste pacote:\*\*.*1\.1\.1/);
  assert.match(portuguese, /confirme a disponibilidade pública no Visual Studio Marketplace/);
  assert.doesNotMatch(`${english}${portuguese}`, /vsix-package-status/);
});

test('prepares the changelog heading for the packaged VSIX', () => {
  assert.equal(
    prepareChangelog(changelog, version),
    `# Changelog\n\n## ${version} — 2026-11-05\n`,
  );
});

test('fails closed when required source markers are missing or duplicated', () => {
  assert.throws(
    () => prepareReadme('# Missing markers\n', 'en', version),
    /Expected exactly one VSIX package status block; found 0/,
  );
  assert.throws(
    () => prepareChangelog(`${changelog}${changelog}`, version),
    /Expected exactly one prepared 1\.1\.1 changelog heading; found 2/,
  );
});

test('packages rendered documents and restores candidate source documents', async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), 'adr-guard-vsix-package-'));
  const originals = new Map([
    ['README.md', englishReadme],
    ['README.pt-BR.md', portugueseReadme],
    ['CHANGELOG.md', changelog],
  ]);

  try {
    await writeFile(path.join(root, 'package.json'), JSON.stringify({ version }), 'utf8');
    await Promise.all(
      [...originals].map(([name, content]) => writeFile(path.join(root, name), content, 'utf8')),
    );

    const status = await packageVsix({
      root,
      packageArguments: ['--out', 'candidate.vsix'],
      executePackage: ({ packageArguments }) => {
        assert.deepEqual(packageArguments, ['--out', 'candidate.vsix']);
        return { status: 0 };
      },
    });

    assert.equal(status, 0);
    for (const [name, content] of originals) {
      assert.equal(await readFile(path.join(root, name), 'utf8'), content);
    }
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test('restores source documents when packaging fails', async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), 'adr-guard-vsix-failure-'));
  const originals = new Map([
    ['README.md', englishReadme],
    ['README.pt-BR.md', portugueseReadme],
    ['CHANGELOG.md', changelog],
  ]);

  try {
    await writeFile(path.join(root, 'package.json'), JSON.stringify({ version }), 'utf8');
    await Promise.all(
      [...originals].map(([name, content]) => writeFile(path.join(root, name), content, 'utf8')),
    );

    await assert.rejects(
      packageVsix({
        root,
        executePackage: () => {
          throw new Error('simulated packaging failure');
        },
      }),
      /simulated packaging failure/,
    );
    for (const [name, content] of originals) {
      assert.equal(await readFile(path.join(root, name), 'utf8'), content);
    }
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});
