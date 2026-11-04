import { spawnSync } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const extensionRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const repositoryRoot = path.resolve(extensionRoot, '..', '..');
const c8 = path.join(extensionRoot, 'node_modules', 'c8', 'bin', 'c8.js');
const reportDirectory = path.join(repositoryRoot, 'artifacts', 'coverage', 'sonar', 'vscode');
const tests = path.join(extensionRoot, 'out', 'test', 'unit', '*.test.js');

const result = spawnSync(process.execPath, [
  c8,
  '--reporter=lcov',
  `--reports-dir=${reportDirectory}`,
  '--exclude=extensions/vscode/out/test/**',
  process.execPath,
  '--test',
  tests,
], {
  cwd: repositoryRoot,
  encoding: 'utf8',
  stdio: 'inherit',
});

if (result.error !== undefined) throw result.error;
process.exitCode = result.status ?? 1;
