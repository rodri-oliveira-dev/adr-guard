import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { describe, it } from 'node:test';
import { CheckReportError, parseCheckReport } from '../../src/contracts/checkReport';

const fixtures = path.resolve(__dirname, '../../../test/fixtures');

describe('ADR check report v1 parser', () => {
  it('parses realistic valid and invalid reports including ADR005', async () => {
    const valid = parseCheckReport(await readFile(path.join(fixtures, 'check-valid.json'), 'utf8'));
    assert.equal(valid.valid, true);
    assert.equal(valid.diagnostics.length, 0);

    const invalid = parseCheckReport(await readFile(path.join(fixtures, 'check-invalid.json'), 'utf8'));
    assert.equal(invalid.valid, false);
    assert.deepEqual(invalid.diagnostics.map((item) => item.code), ['ADR005', 'ADR003']);
    assert.equal(invalid.diagnostics[0]?.file, 'docs/adr/0002-use-redis.md');
  });

  it('preserves optional baseline state without deriving validity from diagnostics', () => {
    const report = parseCheckReport(JSON.stringify({
      schemaVersion: '1.0',
      valid: true,
      summary: { files: 1, diagnostics: 1 },
      files: ['docs/adr/0001.md'],
      baseline: { new: 0, existing: 1, resolved: 0 },
      diagnostics: [{ code: 'ADR005', message: 'Missing Decision', file: 'docs/adr/0001.md', baselineState: 'existing' }],
    }));
    assert.equal(report.valid, true);
    assert.equal(report.diagnostics[0]?.baselineState, 'existing');
  });

  it('rejects empty, malformed and incompatible output', () => {
    const invalid = [
      '',
      '{',
      JSON.stringify({ schemaVersion: '2.0', valid: true, summary: { files: 0, diagnostics: 0 }, files: [], diagnostics: [] }),
      JSON.stringify({ schemaVersion: '1.0', valid: true, summary: { files: 0, diagnostics: 1 }, files: [], diagnostics: [] }),
      JSON.stringify({ schemaVersion: '1.0', valid: true, summary: { files: 0, diagnostics: 0 }, files: [], diagnostics: [], extra: true }),
      JSON.stringify({ schemaVersion: '1.0', valid: false, summary: { files: 1, diagnostics: 1 }, files: ['a.md'], diagnostics: [{ code: 'BAD', message: 'x', file: 'a.md' }] }),
      JSON.stringify({ schemaVersion: '1.0', valid: true, summary: { files: 1, diagnostics: 1 }, files: ['a.md'], diagnostics: [{ code: 'ADR001', message: 'x', file: 'a.md', baselineState: 'existing' }] }),
      JSON.stringify({ schemaVersion: '1.0', valid: false, summary: { files: 1, diagnostics: 1 }, files: ['a.md'], baseline: { new: 0, existing: 1, resolved: 0 }, diagnostics: [{ code: 'ADR001', message: 'x', file: 'a.md', baselineState: 'existing' }] }),
    ];
    invalid.forEach((value) => assert.throws(() => parseCheckReport(value), CheckReportError));
  });
});
