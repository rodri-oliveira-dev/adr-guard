import assert from 'node:assert/strict';
import { chmod, mkdir, mkdtemp, realpath, rm, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { afterEach, describe, it } from 'node:test';
import { isSafeMcpServerPath, McpServerDiscovery } from '../../src/mcp/discovery';

const temporaryDirectories: string[] = [];

afterEach(async () => {
  await Promise.all(temporaryDirectories.splice(0).map(async (directory) => rm(directory, { recursive: true, force: true })));
});

async function temporaryDirectory(): Promise<string> {
  const directory = await mkdtemp(path.join(os.tmpdir(), 'adr-guard-mcp-vscode-'));
  temporaryDirectories.push(directory);
  return directory;
}

async function executable(directory: string, name: string): Promise<string> {
  const candidate = path.join(directory, name);
  await writeFile(candidate, 'fixture');
  if (process.platform !== 'win32') await chmod(candidate, 0o755);
  return candidate;
}

describe('McpServerDiscovery', () => {
  it('keeps the integration explicitly unconfigured by default', async () => {
    assert.deepEqual(await new McpServerDiscovery().inspect({ workspaceRoots: [] }), { code: 'not-configured' });
  });

  it('builds a dotnet launch only for the real MCP assembly name', async () => {
    const installation = await temporaryDirectory();
    const runtime = await executable(installation, process.platform === 'win32' ? 'dotnet.exe' : 'dotnet');
    const server = path.join(installation, 'adr-guard-mcp.dll');
    await writeFile(server, 'fixture');
    const result = await new McpServerDiscovery().inspect({
      configuredPath: server,
      workspaceRoots: [],
      pathEnvironment: installation,
    });
    assert.equal(result.code, 'available');
    assert.ok(result.launch);
    assert.equal(result.launch.command, await realpath(runtime));
    assert.deepEqual(result.launch.argsPrefix, [await realpath(server)]);
  });

  it('supports the native apphost without using a shell command', async () => {
    const installation = await temporaryDirectory();
    const server = await executable(installation, process.platform === 'win32' ? 'adr-guard-mcp.exe' : 'adr-guard-mcp');
    const result = await new McpServerDiscovery().inspect({ configuredPath: server, workspaceRoots: [] });
    assert.equal(result.code, 'available');
    assert.ok(result.launch);
    assert.equal(result.launch.command, await realpath(server));
    assert.deepEqual(result.launch.argsPrefix, []);
  });

  it('rejects missing, arbitrary, and workspace-controlled binaries', async () => {
    const installation = await temporaryDirectory();
    const workspace = path.join(installation, 'workspace');
    await mkdir(workspace);
    const arbitrary = await executable(installation, process.platform === 'win32' ? 'anything.exe' : 'anything');
    const workspaceServer = await executable(workspace, process.platform === 'win32' ? 'adr-guard-mcp.exe' : 'adr-guard-mcp');
    const discovery = new McpServerDiscovery();
    assert.equal((await discovery.inspect({ configuredPath: path.join(installation, 'missing'), workspaceRoots: [] })).code, 'not-found');
    assert.equal((await discovery.inspect({ configuredPath: arbitrary, workspaceRoots: [] })).code, 'invalid-configuration');
    assert.equal((await discovery.inspect({ configuredPath: workspaceServer, workspaceRoots: [workspace] })).code, 'invalid-configuration');
  });

  it('honors cancellation and rejects relative or Windows network paths', async () => {
    const controller = new AbortController();
    controller.abort();
    assert.equal((await new McpServerDiscovery().inspect({ configuredPath: '/unused', workspaceRoots: [], signal: controller.signal })).code, 'cancelled');
    assert.equal(isSafeMcpServerPath('relative/adr-guard-mcp.dll', 'linux'), false);
    assert.equal(isSafeMcpServerPath('\\\\server\\share\\adr-guard-mcp.dll', 'win32'), false);
    assert.equal(isSafeMcpServerPath('C:\\tools\\adr-guard-mcp.dll', 'win32'), true);
  });
});
