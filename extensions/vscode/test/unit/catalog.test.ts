import assert from 'node:assert/strict';
import { mkdir, mkdtemp, realpath, rm, symlink, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { afterEach, describe, it } from 'node:test';
import { loadAdrCatalog, resolveMarkdownDestination } from '../../src/explorer/catalog';

const temporaryDirectories: string[] = [];

afterEach(async () => {
  await Promise.all(temporaryDirectories.splice(0).map(async (directory) => rm(directory, { recursive: true, force: true })));
});

async function workspace(): Promise<{ root: string; adrs: string }> {
  const root = await mkdtemp(path.join(os.tmpdir(), 'adr-explorer-'));
  temporaryDirectories.push(root);
  const adrs = path.join(root, 'docs', 'adr');
  await mkdir(adrs, { recursive: true });
  return { root, adrs };
}

describe('ADR Explorer catalog', () => {
  it('returns an empty catalog and deterministic recursive ordering', async () => {
    const { root, adrs } = await workspace();
    assert.deepEqual(await loadAdrCatalog(root, adrs, 'canonical'), []);
    await mkdir(path.join(adrs, 'nested'));
    await writeFile(path.join(adrs, 'README.md'), '# index');
    await writeFile(path.join(adrs, '0010-z.md'), canonical('Zed', 'Proposed'));
    await writeFile(path.join(adrs, '0002-a.md'), canonical('Alpha', 'Accepted'));
    await writeFile(path.join(adrs, 'nested', '0003-b.md'), '# malformed but displayable');
    const entries = await loadAdrCatalog(root, adrs, 'canonical');
    assert.deepEqual(entries.map((entry) => entry.relativePath), ['0002-a.md', '0010-z.md', 'nested/0003-b.md']);
    assert.deepEqual(entries.map((entry) => entry.metadata.status), ['Accepted', 'Proposed', undefined]);
  });

  it('resolves only existing ADR links within the configured directory', async () => {
    const { root, adrs } = await workspace();
    const target = path.join(adrs, '0002-target.md');
    const source = path.join(adrs, '0001-source.md');
    await writeFile(target, canonical('Target', 'Accepted'));
    await writeFile(source, `${canonical('Source', 'Accepted')}\n## Dependencies\n[Target](0002-target.md)\n[Missing](missing.md)\n[External](../../../outside.md)\n`);
    const entries = await loadAdrCatalog(root, adrs, 'canonical');
    const sourceEntry = entries[0];
    assert.ok(sourceEntry);
    assert.equal(sourceEntry.relationships.length, 1);
    assert.equal(sourceEntry.relationships[0]?.targetPath, await realpath(target));
    assert.equal(await resolveMarkdownDestination(source, 'https://example.com/a.md', adrs), undefined);
    assert.equal(await resolveMarkdownDestination(source, 'C:\\outside.md', adrs), undefined);
  });

  it('rejects symlink escapes and resolves MADR status only when the ID is unique', async () => {
    const { root, adrs } = await workspace();
    const outside = await mkdtemp(path.join(os.tmpdir(), 'adr-explorer-outside-'));
    temporaryDirectories.push(outside);
    await writeFile(path.join(outside, '0099-outside.md'), canonical('Outside', 'Accepted'));
    await symlink(outside, path.join(adrs, 'escape'), process.platform === 'win32' ? 'junction' : 'dir');
    await writeFile(path.join(adrs, '0001-source.md'), `${canonical('Source', 'Accepted')}\n## References\n[Escape](escape/0099-outside.md)`);
    await writeFile(path.join(adrs, '0042-target.md'), canonical('Target', 'Accepted'));
    await writeFile(path.join(adrs, '0003-madr.md'), '---\nstatus: superseded by ADR-0042\n---\n# MADR\n## Decision Outcome\nChoice.');
    const entries = await loadAdrCatalog(root, adrs, 'madr-4');
    assert.equal(entries.find((entry) => entry.fileName === '0001-source.md')?.relationships.length, 0);
    assert.equal(entries.find((entry) => entry.fileName === '0003-madr.md')?.relationships[0]?.targetPath,
      await realpath(path.join(adrs, '0042-target.md')));
  });
});

function canonical(title: string, status: string): string {
  return `# ${title}\n## Status\n${status}\n## Context\nContext.\n## Decision\nDecision.\n## Consequences\nConsequences.\n`;
}
