import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { classifyExitCode } from '../../src/contracts/cli';

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
