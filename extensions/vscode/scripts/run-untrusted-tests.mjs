import { spawn } from 'node:child_process';
import { mkdtemp, rm } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { createRequire } from 'node:module';
import { downloadAndUnzipVSCode } from '@vscode/test-electron';

const root = path.resolve(import.meta.dirname, '..');
const require = createRequire(import.meta.url);
const testCliDirectory = path.dirname(require.resolve('@vscode/test-cli'));
const extensionTestsPath = path.join(testCliDirectory, 'runner.cjs');
const testFile = path.join(root, 'out', 'test', 'integration', 'trust.test.js');
const workspaceFolder = path.join(root, 'test', 'fixtures', 'workspace');
const userDataDirectory = await mkdtemp(path.join(os.tmpdir(), 'adr-guard-vscode-untrusted-'));
const extensionsDirectory = await mkdtemp(path.join(os.tmpdir(), 'adr-guard-vscode-untrusted-extensions-'));

try {
  const executable = await downloadAndUnzipVSCode({ version: '1.100.0' });
  const testOptions = JSON.stringify({
    mochaOpts: { timeout: 20000 },
    colorDefault: true,
    preload: [],
    files: [testFile],
  });
  const args = [
    '--no-sandbox',
    '--disable-gpu-sandbox',
    '--disable-updates',
    '--skip-welcome',
    '--skip-release-notes',
    '--no-cached-data',
    `--user-data-dir=${userDataDirectory}`,
    `--extensions-dir=${extensionsDirectory}`,
    '--disable-extensions',
    `--extensionDevelopmentPath=${root}`,
    `--extensionTestsPath=${extensionTestsPath}`,
    workspaceFolder,
  ];
  const exitCode = await new Promise((resolve, reject) => {
    const child = spawn(executable, args, {
      stdio: 'inherit',
      env: {
        ...process.env,
        ELECTRON_RUN_AS_NODE: undefined,
        VSCODE_TEST_OPTIONS: testOptions,
      },
    });
    child.once('error', reject);
    child.once('close', (code) => resolve(code ?? 1));
  });
  if (exitCode !== 0) process.exitCode = exitCode;
} finally {
  await Promise.all([
    rm(userDataDirectory, { recursive: true, force: true, maxRetries: 3 }),
    rm(extensionsDirectory, { recursive: true, force: true, maxRetries: 3 }),
  ]);
}
