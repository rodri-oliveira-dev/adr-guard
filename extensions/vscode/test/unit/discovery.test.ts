import assert from 'node:assert/strict';
import { chmod, mkdir, mkdtemp, realpath, rm, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { afterEach, describe, it } from 'node:test';
import { CliDiscovery, CliDiscoveryError } from '../../src/cli/discovery';

const temporaryDirectories: string[] = [];

afterEach(async () => {
  await Promise.all(temporaryDirectories.splice(0).map(async (directory) => rm(directory, { recursive: true, force: true })));
});

async function temporaryDirectory(): Promise<string> {
  const directory = await mkdtemp(path.join(os.tmpdir(), 'adr-guard-vscode-'));
  temporaryDirectories.push(directory);
  return directory;
}

async function fakeExecutable(directory: string): Promise<string> {
  const name = process.platform === 'win32' ? 'adr-guard.exe' : 'adr-guard';
  const candidate = path.join(directory, name);
  await writeFile(candidate, 'fixture');
  if (process.platform !== 'win32') {
    await chmod(candidate, 0o755);
  }
  return candidate;
}

describe('CliDiscovery', () => {
  it('prefers a valid explicit absolute executable', async () => {
    const directory = await temporaryDirectory();
    const configured = await fakeExecutable(directory);
    const result = await new CliDiscovery().find({ configuredPath: configured, pathEnvironment: '' });
    assert.equal(result.path, await realpath(configured));
    assert.equal(result.source, 'configured');
  });

  it('does not fall back when an explicit path is invalid', async () => {
    const directory = await temporaryDirectory();
    await fakeExecutable(directory);
    await assert.rejects(
      new CliDiscovery().find({ configuredPath: path.join(directory, 'missing'), pathEnvironment: directory }),
      (error: unknown) => error instanceof CliDiscoveryError && error.code === 'invalid-configured-path',
    );
  });

  it('finds the CLI on the extension host PATH', async () => {
    const directory = await temporaryDirectory();
    const executable = await fakeExecutable(directory);
    const result = await new CliDiscovery().find({ pathEnvironment: directory });
    assert.equal(result.path, await realpath(executable));
    assert.equal(result.source, 'path');
  });

  it('ignores PATH executables controlled by the workspace', async () => {
    const root = await temporaryDirectory();
    const bin = path.join(root, 'bin');
    await mkdir(bin);
    await fakeExecutable(bin);
    await assert.rejects(
      new CliDiscovery().find({ pathEnvironment: bin, workspaceRoots: [root] }),
      (error: unknown) => error instanceof CliDiscoveryError && error.code === 'not-found',
    );
  });

  it('rejects relative configured paths and missing CLI installations', async () => {
    const discovery = new CliDiscovery();
    await assert.rejects(
      discovery.find({ configuredPath: './adr-guard' }),
      (error: unknown) => error instanceof CliDiscoveryError && error.code === 'invalid-configured-path',
    );
    await assert.rejects(
      discovery.find({ pathEnvironment: '' }),
      (error: unknown) => error instanceof CliDiscoveryError && error.code === 'not-found',
    );
  });

  it('rejects network executable paths on Windows', async () => {
    await assert.rejects(
      new CliDiscovery().find({ configuredPath: '\\\\server\\share\\adr-guard.exe', platform: 'win32' }),
      (error: unknown) => error instanceof CliDiscoveryError && error.code === 'invalid-configured-path',
    );
  });
});
