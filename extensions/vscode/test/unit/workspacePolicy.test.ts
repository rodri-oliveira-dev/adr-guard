import assert from 'node:assert/strict';
import { mkdtemp, mkdir, realpath, rm, symlink, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { afterEach, describe, it } from 'node:test';
import {
  eligibleWorkspaceFolders,
  ensureResourceWithinWorkspace,
  isResolvedPathWithin,
  preferredWorkspaceLocation,
  resolveWorkspaceRoot,
  WorkspacePolicyError,
} from '../../src/workspacePolicy';

const temporaryDirectories: string[] = [];

afterEach(async () => {
  await Promise.all(temporaryDirectories.splice(0).map(async (directory) => rm(directory, { recursive: true, force: true })));
});

async function temporaryDirectory(): Promise<string> {
  const directory = await mkdtemp(path.join(os.tmpdir(), 'adr-guard-workspace-'));
  temporaryDirectories.push(directory);
  return directory;
}

describe('workspace policy', () => {
  it('uses host-appropriate case sensitivity for resolved boundaries', () => {
    const root = path.resolve('C:\\Workspace');
    assert.equal(
      isResolvedPathWithin(path.join(root.toLowerCase(), 'docs', 'adr'), root),
      process.platform === 'win32',
    );
  });

  it('uses the active folder in a multi-root workspace and otherwise requests a choice', () => {
    const folders = [
      { name: 'one', scheme: 'file', fsPath: 'C:\\one' },
      { name: 'two', scheme: 'file', fsPath: 'C:\\two' },
    ];
    assert.equal(preferredWorkspaceLocation(folders, 'C:\\two')?.name, 'two');
    assert.equal(preferredWorkspaceLocation(folders, undefined), undefined);
    assert.equal(preferredWorkspaceLocation([folders[0]!], undefined)?.name, 'one');
  });
  it('blocks untrusted workspaces before inspecting folders', () => {
    assert.throws(
      () => eligibleWorkspaceFolders(false, []),
      (error: unknown) => error instanceof WorkspacePolicyError && error.code === 'untrusted',
    );
  });

  it('rejects absence of a workspace and virtual resources', () => {
    assert.throws(
      () => eligibleWorkspaceFolders(true, undefined),
      (error: unknown) => error instanceof WorkspacePolicyError && error.code === 'missing',
    );
    assert.throws(
      () => eligibleWorkspaceFolders(true, [{ name: 'virtual', scheme: 'vscode-vfs', fsPath: '/virtual' }]),
      (error: unknown) => error instanceof WorkspacePolicyError && error.code === 'non-file',
    );
  });

  it('accepts accessible local workspace folders', async () => {
    const root = await temporaryDirectory();
    const folders = eligibleWorkspaceFolders(true, [{ name: 'local', scheme: 'file', fsPath: root }]);
    assert.equal(await resolveWorkspaceRoot(folders[0]!), await realpath(root));
  });

  it('rejects resources and symlinks escaping the workspace', async () => {
    const root = await temporaryDirectory();
    const outside = await temporaryDirectory();
    const outsideFile = path.join(outside, 'outside.md');
    await writeFile(outsideFile, '# outside');
    await assert.rejects(
      ensureResourceWithinWorkspace(outsideFile, root),
      (error: unknown) => error instanceof WorkspacePolicyError && error.code === 'outside-workspace',
    );

    const nested = path.join(root, 'adrs');
    await mkdir(nested);
    const localFile = path.join(nested, '0001.md');
    await writeFile(localFile, '# local');
    assert.equal(await ensureResourceWithinWorkspace(localFile, root), await realpath(localFile));

    const link = path.join(root, 'escaped-directory');
    await symlink(outside, link, process.platform === 'win32' ? 'junction' : 'dir');
    await assert.rejects(
      ensureResourceWithinWorkspace(path.join(link, 'outside.md'), root),
      (error: unknown) => error instanceof WorkspacePolicyError && error.code === 'outside-workspace',
    );
  });
});
