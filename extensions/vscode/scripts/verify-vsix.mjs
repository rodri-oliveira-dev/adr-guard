import { spawn } from 'node:child_process';
import { access, mkdtemp, rm } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { createRequire } from 'node:module';
import { downloadAndUnzipVSCode, runVSCodeCommand } from '@vscode/test-electron';

const root = path.resolve(import.meta.dirname, '..');
const vsixPath = path.join(root, 'adr-guard-0.1.0.vsix');
const workspaceFolder = path.join(root, 'test', 'fixtures', 'workspace');
const testHarness = path.join(root, 'test', 'fixtures', 'extension-test-harness');
const testFile = path.join(root, 'out', 'test', 'integration', 'packagedActivation.test.js');
const require = createRequire(import.meta.url);
const extensionTestsPath = path.join(path.dirname(require.resolve('@vscode/test-cli')), 'runner.cjs');
const profileRoot = await mkdtemp(path.join(os.tmpdir(), 'adr-guard-vsix-smoke-'));
const userDataDirectory = path.join(profileRoot, 'user-data');
const extensionsDirectory = path.join(profileRoot, 'extensions');

await access(vsixPath);

try {
  const profileArgs = [
    `--user-data-dir=${userDataDirectory}`,
    `--extensions-dir=${extensionsDirectory}`,
  ];
  await runVSCodeCommand(
    [...profileArgs, '--install-extension', vsixPath, '--force'],
    { version: '1.100.0' },
  );
  const installed = await runVSCodeCommand(
    [...profileArgs, '--list-extensions', '--show-versions'],
    { version: '1.100.0' },
  );
  if (!installed.stdout.toLowerCase().includes('adr-guard@0.1.0')) {
    throw new Error(`Installed extension list does not contain ADR Guard 0.1.0: ${installed.stdout}`);
  }

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
    '--disable-workspace-trust',
    ...profileArgs,
    `--extensionDevelopmentPath=${testHarness}`,
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
  await rm(profileRoot, { recursive: true, force: true, maxRetries: 3 });
}
