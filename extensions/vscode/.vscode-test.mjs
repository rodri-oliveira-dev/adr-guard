import { defineConfig } from '@vscode/test-cli';
import { mkdtempSync, rmSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';

const root = import.meta.dirname;
const profiles = [];
const isolatedProfile = (label) => {
  const profile = mkdtempSync(path.join(os.tmpdir(), `adr-guard-vscode-${label}-`));
  profiles.push(profile);
  return profile;
};
process.once('exit', () => {
  for (const profile of profiles) rmSync(profile, { recursive: true, force: true, maxRetries: 3 });
});
const trustedLaunchArgs = (label) => [
  `--user-data-dir=${isolatedProfile(label)}`,
  '--disable-workspace-trust',
  '--disable-extensions',
  '--skip-welcome',
  '--skip-release-notes',
];

export default defineConfig([
  {
    label: 'trusted-single-root',
    files: [
      'out/test/integration/activation.test.js',
      'out/test/integration/diagnostics.test.js',
      'out/test/integration/explorer.test.js',
    ],
    version: '1.100.0',
    extensionDevelopmentPath: root,
    workspaceFolder: path.join(root, 'test', 'fixtures', 'workspace'),
    mocha: {
      timeout: 20000,
    },
    launchArgs: trustedLaunchArgs('trusted-single-root'),
  },
  {
    label: 'trusted-multi-root',
    files: 'out/test/integration/multiRoot.test.js',
    version: '1.100.0',
    extensionDevelopmentPath: root,
    workspaceFolder: path.join(root, 'test', 'fixtures', 'multi-root.code-workspace'),
    mocha: {
      timeout: 20000,
    },
    launchArgs: trustedLaunchArgs('trusted-multi-root'),
  },
]);
