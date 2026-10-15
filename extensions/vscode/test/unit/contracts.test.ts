import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { classifyExitCode } from '../../src/contracts/cli';
import { parseNewAdrFormResponse } from '../../src/contracts/newForm';
import { parseEditFormResponse } from '../../src/contracts/editForm';

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

describe('ADR edit form contracts', () => {
  const valid = {
    schemaVersion: '1.0',
    operation: 'load',
    format: 'canonical',
    path: '/workspace/docs/adr/0001-choice.md',
    fingerprint: 'A'.repeat(64),
    title: 'Choice',
    fields: [
      { id: 'section-0', label: 'Status', kind: 'status', required: true, value: 'Proposed', options: ['Proposed', 'Accepted'] },
      { id: 'section-1', label: 'Context', kind: 'section', required: true, value: 'Context.' },
    ],
    written: false,
  };

  it('accepts bounded dynamic fields for supported ADR formats', () => {
    const response = parseEditFormResponse(JSON.stringify(valid));
    if (response.operation !== 'load') assert.fail('Expected a load response.');
    assert.equal(response.fields[0]?.kind, 'status');
    assert.equal(response.fingerprint, 'A'.repeat(64));
    const acknowledgement = { schemaVersion: '1.0', operation: 'save', fingerprint: 'B'.repeat(64), written: true };
    assert.equal(parseEditFormResponse(JSON.stringify(acknowledgement)).written, true);
  });

  it('rejects incompatible, duplicated, and inconsistent edit responses', () => {
    for (const response of [
      { ...valid, schemaVersion: '2.0' },
      { ...valid, written: true },
      { ...valid, fields: [...valid.fields, valid.fields[0]] },
      { ...valid, fields: [{ ...valid.fields[0], id: '../outside' }] },
      { ...valid, unexpected: true },
      { schemaVersion: '1.0', operation: 'save', fingerprint: 'A'.repeat(64), written: true, fields: [] },
    ]) assert.throws(() => parseEditFormResponse(JSON.stringify(response)));
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
