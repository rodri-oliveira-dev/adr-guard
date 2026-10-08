import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { buildInitArguments, buildNewArguments, parseCreatedAdrPath } from '../../src/commands/arguments';

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
});
