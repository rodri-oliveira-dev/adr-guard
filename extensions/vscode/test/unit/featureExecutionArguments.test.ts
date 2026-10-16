import assert from 'node:assert/strict';
import { mkdtemp, rm, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { afterEach, describe, it } from 'node:test';
import { featureExecutionArguments } from '../../src/commands/featureExecutionArguments';

const temporaryDirectories: string[] = [];

afterEach(async () => {
  await Promise.all(temporaryDirectories.splice(0).map(async (directory) => rm(directory, { recursive: true, force: true })));
});

async function temporaryRepository(): Promise<string> {
  const directory = await mkdtemp(path.join(os.tmpdir(), 'adr-guard-features-'));
  temporaryDirectories.push(directory);
  return directory;
}

const missingValueError = (label: string): Error => new Error(`missing ${label}`);

describe('visual feature execution arguments', () => {
  it('keeps impact and drift file operands repository-relative', async () => {
    const root = await temporaryRepository();
    await writeFile(path.join(root, '.adrguard-impact.json'), '{}');
    await writeFile(path.join(root, '.adrguard-fitness.json'), '{}');
    await writeFile(path.join(root, 'evidence.json'), '{}');

    const impact = await featureExecutionArguments({
      feature: 'impact', workspace: root, baseReference: 'main', manifest: '.adrguard-impact.json',
    }, root, false, missingValueError);
    const drift = await featureExecutionArguments({
      feature: 'drift', workspace: root, rules: '.adrguard-fitness.json', evidence: ['evidence.json'],
    }, root, false, missingValueError);

    assert.equal(impact[impact.indexOf('--map') + 1], '.adrguard-impact.json');
    assert.equal(drift[drift.indexOf('--rules') + 1], '.adrguard-fitness.json');
    assert.equal(drift[drift.indexOf('--evidence') + 1], 'evidence.json');
  });

  it('does not require the ADR directory for analysis or review workflows', async () => {
    const root = await temporaryRepository();
    await writeFile(path.join(root, '.adrguard-impact.json'), '{}');
    await writeFile(path.join(root, '0001-decision.md'), '# Decision');

    await assert.doesNotReject(featureExecutionArguments({
      feature: 'impact', workspace: root, baseReference: 'main', manifest: '.adrguard-impact.json',
    }, root, false, missingValueError));
    await assert.doesNotReject(featureExecutionArguments({
      feature: 'review', workspace: root, targetAdr: '0001-decision.md', provider: 'openai', model: 'test',
    }, root, false, missingValueError));
  });
});
