import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  buildCheckArguments,
  buildIndexArguments,
  buildInitArguments,
  buildNewArguments,
  isSafeGitBaseReference,
  parseCreatedAdrPath,
} from '../../src/commands/arguments';

describe('ADR command arguments', () => {
  it('builds init dry-run and materialization arguments without implicit overwrite', () => {
    const preview = buildInitArguments({
      repository: 'C:\\repo',
      adrDirectory: 'architecture/decisions',
      template: 'extended',
      githubActions: true,
      dryRun: true,
    });
    assert.deepEqual(preview, [
      'init', 'C:\\repo', '--adr-directory', 'architecture/decisions',
      '--template', 'extended', '--github-actions', '--dry-run',
    ]);
    assert.equal(preview.includes('--overwrite'), false);
    assert.equal(buildInitArguments({ ...{
      repository: 'C:\\repo', adrDirectory: 'docs/adr', template: 'configured' as const,
      githubActions: false, dryRun: false,
    } }).includes('--overwrite'), false);
  });

  it('passes title, templates, culture and special characters as literal arguments', () => {
    assert.deepEqual(buildNewArguments({
      adrDirectory: 'C:\\repo with spaces\\docs\\adr',
      title: 'Use Redis; $(literal)',
      template: { file: 'C:\\repo\\template.md' },
      culture: 'pt-BR',
    }), [
      'new', 'C:\\repo with spaces\\docs\\adr', '--title', 'Use Redis; $(literal)',
      '--template-file', 'C:\\repo\\template.md', '--culture', 'pt-BR',
    ]);
  });

  it('accepts only the documented new-command output shape', () => {
    assert.equal(parseCreatedAdrPath('ADR written: C:\\repo\\docs\\adr\\0001-use-redis.md\n'), 'C:\\repo\\docs\\adr\\0001-use-redis.md');
    for (const output of ['', 'Created C:\\outside.md', 'ADR written: ../outside.md\nextra', 'ADR written:  C:\\bad.md']) {
      assert.throws(() => parseCreatedAdrPath(output), /unsafe or unexpected/u);
    }
  });

  it('keeps canonical compatible and separates ADR format from JSON output format', () => {
    assert.deepEqual(buildCheckArguments({ directory: '/repo/docs/adr', adrFormat: 'canonical' }), [
      'check', '/repo/docs/adr', '--format', 'json',
    ]);
    assert.deepEqual(buildCheckArguments({
      directory: '/repo/docs/adr',
      adrFormat: 'madr-4',
      changed: true,
      baseReference: 'origin/main',
      baselinePath: '/repo/.adrguard-baseline.json',
    }), [
      'check', '/repo/docs/adr', '--adr-format', 'madr-4', '--changed', '--base-ref', 'origin/main',
      '--baseline', '/repo/.adrguard-baseline.json', '--format', 'json',
    ]);
    assert.deepEqual(buildIndexArguments('/repo/docs/adr', 'madr-4'), [
      'index', '/repo/docs/adr', '--adr-format', 'madr-4',
    ]);
    assert.throws(() => buildCheckArguments({ directory: '/repo', adrFormat: 'canonical', changed: true }));
  });

  it('accepts ordinary Git refs and rejects option/ref injection syntax', () => {
    for (const reference of ['main', 'origin/main', 'release/1.4.0', 'feature_111', 'HEAD~1', 'branch@{upstream}', '$(literal)']) {
      assert.equal(isSafeGitBaseReference(reference), true, reference);
    }
    for (const reference of ['', '--help', 'main other', "main\nother", `main${String.fromCharCode(0)}other`, 'x'.repeat(257)]) {
      assert.equal(isSafeGitBaseReference(reference), false, reference);
    }
  });
});
