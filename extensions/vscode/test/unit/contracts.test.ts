import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { classifyExitCode } from '../../src/contracts/cli';
import { parseNewAdrFormResponse } from '../../src/contracts/newForm';

describe('CLI exit contracts', () => {
  it('normalizes all documented CLI exit codes', () => {
    assert.deepEqual([0, 1, 2, 3, 4, 99].map(classifyExitCode), [
      'success',
      'validation-failed',
      'usage-error',
      'operational-error',
      'policy-failed',
      'unknown',
    ]);
  });
});

describe('visual ADR form contracts', () => {
  it('accepts the strict schema 1.0 response', () => {
    const response = parseNewAdrFormResponse(JSON.stringify({
      schemaVersion: '1.0',
      operation: 'preview',
      template: {
        name: 'minimal',
        compatible: true,
        fields: [
          { id: 'context', required: true },
          { id: 'decision', required: true },
          { id: 'consequences', required: true },
        ],
      },
      path: '/workspace/docs/adr/0001-example.md',
      markdown: '# Example',
      written: false,
    }));
    assert.equal(response.template.name, 'minimal');
    assert.equal(response.markdown, '# Example');
  });

  it('rejects incompatible, unexpected, and malicious shapes', () => {
    const invalid = [
      '{not json',
      JSON.stringify({ schemaVersion: '2.0', operation: 'describe', template: {}, written: false }),
      JSON.stringify({ schemaVersion: '1.0', operation: 'describe', template: { name: 'minimal', compatible: true, fields: [] }, written: false, unexpected: true }),
      JSON.stringify({ schemaVersion: '1.0', operation: 'preview', template: { name: 'minimal', compatible: true, fields: [{ id: '<script>', required: true }] }, markdown: '<script>', written: false }),
    ];
    for (const value of invalid) assert.throws(() => parseNewAdrFormResponse(value));
  });
});
