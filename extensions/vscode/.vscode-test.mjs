import { defineConfig } from '@vscode/test-cli';
import path from 'node:path';

const root = import.meta.dirname;

export default defineConfig({
  files: 'out/test/integration/**/*.test.js',
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
});
