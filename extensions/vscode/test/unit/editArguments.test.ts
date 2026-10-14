import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { buildVisualEditArguments, supportsAdrDirectory } from '../../src/commands/editArguments';

describe('visual edit CLI arguments', () => {
  it('passes the configured ADR root when the CLI advertises support', () => {
    const help = 'adr-guard edit <adr-file> --form [--adr-directory <directory>] [--adr-format canonical|madr-4]';

    assert.equal(supportsAdrDirectory(help), true);
    assert.deepEqual(
      buildVisualEditArguments('/repo/docs/adr/0001-test.md', 'docs/adr', 'canonical', help),
      ['edit', '/repo/docs/adr/0001-test.md', '--form', '--adr-directory', 'docs/adr', '--adr-format', 'canonical'],
    );
  });

  it('keeps visual editing compatible with CLI 1.7.0', () => {
    const help = 'adr-guard edit <adr-file> --form [--adr-format canonical|madr-4]';

    assert.equal(supportsAdrDirectory(help), false);
    assert.deepEqual(
      buildVisualEditArguments('C:\\repo\\docs\\adr\\0001-test.md', 'docs/adr', 'madr-4', help),
      ['edit', 'C:\\repo\\docs\\adr\\0001-test.md', '--form', '--adr-format', 'madr-4'],
    );
  });

  it('does not accept the option name as part of unrelated text', () => {
    assert.equal(supportsAdrDirectory('unsupported: --adr-directory-suffix'), false);
  });
});
