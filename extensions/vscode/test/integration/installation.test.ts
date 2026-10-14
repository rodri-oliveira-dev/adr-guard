import assert from 'node:assert/strict';
import * as vscode from 'vscode';
import type { CliDiscovery } from '../../src/cli/discovery';
import type { CliRunRequest, CliRunner } from '../../src/cli/runner';
import { checkInstallation } from '../../src/commands/checkInstallation';
import type { OperationalLog } from '../../src/logging';

suite('ADR Guard installation settings integration', () => {
  test('runs the version check only when explicitly requested and returns the detected version', async () => {
    const folder = vscode.workspace.workspaceFolders?.[0];
    assert.ok(folder);
    let invocation: CliRunRequest | undefined;
    const result = await checkInstallation({
      discovery: {
        find: () => Promise.resolve({ path: process.execPath, source: 'path' as const }),
      } as unknown as CliDiscovery,
      runner: {
        run: (options: CliRunRequest) => {
          invocation = options;
          return Promise.resolve({
            termination: 'exited' as const,
            exitCode: 0,
            exitKind: 'success' as const,
            stdout: '1.7.0\n',
            stderr: '',
            signal: null,
          });
        },
      } as unknown as CliRunner,
      log: { info: () => undefined } as unknown as OperationalLog,
    }, folder, false);

    assert.deepEqual(result, {
      available: true,
      message: 'ADR Guard 1.7.0 is available (path).',
      version: '1.7.0',
      source: 'path',
    });
    assert.ok(invocation);
    assert.deepEqual(invocation.args, ['--version']);
    assert.equal(invocation.timeoutMilliseconds, 15000);
    assert.equal(invocation.maxOutputBytes, 262144);
  });
});
