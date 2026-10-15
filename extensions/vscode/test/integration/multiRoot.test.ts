import assert from 'node:assert/strict';
import { chmod, mkdtemp, realpath, rm, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import * as vscode from 'vscode';
import type { OperationalLog } from '../../src/logging';
import { AdrExplorerProvider, AdrNode, WorkspaceNode } from '../../src/explorer/provider';

suite('ADR Guard Explorer multi-root', () => {
  test('groups workspace folders and presents a clear empty state', async () => {
    const provider = new AdrExplorerProvider({ info: () => undefined } as unknown as OperationalLog);
    try {
      const roots = await provider.getChildren();
      assert.equal(roots.length, 2);
      assert.ok(roots.every((item) => item instanceof WorkspaceNode));
      const populated = roots.find((item) => item instanceof WorkspaceNode && item.folder.name === 'with-adrs');
      const empty = roots.find((item) => item instanceof WorkspaceNode && item.folder.name === 'empty-adrs');
      assert.ok(populated instanceof WorkspaceNode);
      assert.ok(empty instanceof WorkspaceNode);
      const populatedChildren = await provider.getChildren(populated);
      assert.equal(populatedChildren.length, 1);
      assert.ok(populatedChildren[0] instanceof AdrNode);
      const emptyChildren = await provider.getChildren(empty);
      assert.equal(emptyChildren.length, 1);
      const label = emptyChildren[0]?.label;
      assert.match(typeof label === 'string' ? label : label?.label ?? '', /No ADR Markdown files/u);
    } finally {
      provider.dispose();
    }
  });

  test('publishes one isolated stdio MCP definition per enabled workspace folder', async () => {
    const folders = vscode.workspace.workspaceFolders ?? [];
    assert.equal(folders.length, 2);
    const installation = await mkdtemp(path.join(os.tmpdir(), 'adr-guard-mcp-provider-'));
    const serverPath = path.join(installation, process.platform === 'win32' ? 'adr-guard-mcp.exe' : 'adr-guard-mcp');
    await writeFile(serverPath, 'fixture');
    if (process.platform !== 'win32') await chmod(serverPath, 0o755);
    const global = vscode.workspace.getConfiguration('adrGuard.mcp');
    try {
      await global.update('path', serverPath, vscode.ConfigurationTarget.Global);
      await global.update('enabled', true, vscode.ConfigurationTarget.Global);
      const extension = vscode.extensions.all.find((candidate) => candidate.packageJSON.name === 'adr-guard');
      assert.ok(extension);
      const api = await extension.activate() as {
        mcpProvider: vscode.McpServerDefinitionProvider<vscode.McpStdioServerDefinition>;
      };
      const cancellation = new vscode.CancellationTokenSource();
      const definitions = await api.mcpProvider.provideMcpServerDefinitions(cancellation.token);
      cancellation.dispose();
      assert.ok(definitions);
      assert.equal(definitions.length, 2);
      assert.equal(new Set(definitions.map((definition) => definition.label)).size, 2);
      for (const folder of folders) {
        const definition: vscode.McpStdioServerDefinition | undefined = definitions.find(
          (candidate: vscode.McpStdioServerDefinition) => candidate.cwd !== undefined
            && samePath(candidate.cwd.fsPath, folder.uri.fsPath),
        );
        assert.ok(definition, folder.name);
        assert.equal(definition.command, await realpath(serverPath));
        const repositoryIndex = definition.args.indexOf('--repository');
        assert.ok(repositoryIndex >= 0);
        assert.equal(samePath(definition.args[repositoryIndex + 1] ?? '', folder.uri.fsPath), true);
        assert.ok(definition.args.includes('--adr-directory'));
        assert.equal(Object.keys(definition.env).length, 0);
      }
      const first = definitions[0];
      assert.ok(first);
      const resolveCancellation = new vscode.CancellationTokenSource();
      const resolved = await api.mcpProvider.resolveMcpServerDefinition?.(first, resolveCancellation.token);
      resolveCancellation.dispose();
      assert.equal(resolved?.label, first.label);
      const cancelled = new vscode.CancellationTokenSource();
      cancelled.cancel();
      assert.deepEqual(await api.mcpProvider.provideMcpServerDefinitions(cancelled.token), []);
      cancelled.dispose();
    } finally {
      await global.update('path', undefined, vscode.ConfigurationTarget.Global);
      await global.update('enabled', undefined, vscode.ConfigurationTarget.Global);
      await rm(installation, { recursive: true, force: true });
    }
  });
});

function samePath(left: string, right: string): boolean {
  const normalize = (value: string): string => process.platform === 'win32'
    ? path.resolve(value).toLowerCase()
    : path.resolve(value);
  return normalize(left) === normalize(right);
}
