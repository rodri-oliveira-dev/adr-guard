import { defineConfig } from '@vscode/test-cli';
import path from 'node:path';

const root = import.meta.dirname;

export default defineConfig([
  {
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
    launchArgs: [
      '--disable-extensions',
      '--skip-welcome',
      '--skip-release-notes',
    ],
  },
  {
    files: 'out/test/integration/multiRoot.test.js',
    version: '1.100.0',
    extensionDevelopmentPath: root,
    workspaceFolder: path.join(root, 'test', 'fixtures', 'multi-root.code-workspace'),
    mocha: {
      timeout: 20000,
    },
    launchArgs: [
      '--disable-extensions',
      '--skip-welcome',
      '--skip-release-notes',
    ],
  },
]);
