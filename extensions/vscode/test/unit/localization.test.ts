import assert from 'node:assert/strict';
import { readFile, readdir } from 'node:fs/promises';
import path from 'node:path';
import { describe, it } from 'node:test';

const root = path.resolve(__dirname, '../../..');

describe('localization resources', () => {
  it('keeps manifest translation keys and placeholders aligned', async () => {
    const english = JSON.parse(await readFile(path.join(root, 'package.nls.json'), 'utf8')) as Record<string, string>;
    const portuguese = JSON.parse(await readFile(path.join(root, 'package.nls.pt-br.json'), 'utf8')) as Record<string, string>;
    assert.deepEqual(Object.keys(portuguese).sort(), Object.keys(english).sort());
    for (const key of Object.keys(english)) {
      assert.deepEqual(placeholders(portuguese[key] ?? ''), placeholders(english[key] ?? ''), key);
    }
  });

  it('preserves runtime localization placeholders', async () => {
    const portuguese = JSON.parse(await readFile(path.join(root, 'l10n/bundle.l10n.pt-br.json'), 'utf8')) as Record<string, string>;
    for (const [english, translated] of Object.entries(portuguese)) {
      assert.deepEqual(placeholders(translated), placeholders(english), english);
    }
  });

  it('contains a pt-BR entry for every literal runtime message', async () => {
    const portuguese = JSON.parse(await readFile(path.join(root, 'l10n/bundle.l10n.pt-br.json'), 'utf8')) as Record<string, string>;
    const sourceFiles = await typescriptFiles(path.join(root, 'src'));
    const keys = new Set<string>();
    for (const file of sourceFiles) {
      const source = await readFile(file, 'utf8');
      for (const match of source.matchAll(/vscode\.l10n\.t\('([^']+)'/gu)) keys.add(match[1] ?? '');
    }
    const missing = [...keys].filter((key) => !(key in portuguese));
    assert.deepEqual(missing, []);
  });
});

function placeholders(value: string): string[] {
  return value.match(/\{[0-9]+\}/gu)?.sort() ?? [];
}

async function typescriptFiles(directory: string): Promise<string[]> {
  const entries = await readdir(directory, { withFileTypes: true });
  const nested = await Promise.all(entries.map(async (entry) => {
    const candidate = path.join(directory, entry.name);
    if (entry.isDirectory()) return typescriptFiles(candidate);
    return entry.isFile() && candidate.endsWith('.ts') ? [candidate] : [];
  }));
  return nested.flat();
}
