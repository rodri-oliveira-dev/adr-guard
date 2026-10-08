import assert from 'node:assert/strict';
import { rename, rm, writeFile } from 'node:fs/promises';
import { setTimeout as delay } from 'node:timers/promises';
import * as vscode from 'vscode';
import type { OperationalLog } from '../../src/logging';
import { AdrExplorerProvider, AdrNode } from '../../src/explorer/provider';

suite('ADR Guard Explorer', () => {
  test('lists the configured workspace ADR and exposes native open navigation', async () => {
    const provider = new AdrExplorerProvider({ info: () => undefined } as unknown as OperationalLog);
    try {
      const children = await provider.getChildren();
      assert.equal(children.length, 1);
      const adr = children[0];
      assert.ok(adr instanceof AdrNode);
      const label = typeof adr.label === 'string' ? adr.label : adr.label?.label ?? '';
      assert.match(label, /0001.*Invalid/u);
      assert.equal(adr.description, 'Proposed');
      assert.ok(adr.resourceUri);
      assert.equal(adr.resourceUri.scheme, 'file');
      assert.equal(adr.command?.command, 'adrGuard.openAdr');
      await vscode.commands.executeCommand('adrGuard.openAdr', adr.resourceUri);
      assert.equal(vscode.window.activeTextEditor?.document.uri.toString(), adr.resourceUri.toString());
    } finally {
      provider.dispose();
    }
  });

  test('manual refresh and disposal are safe', async () => {
    const provider = new AdrExplorerProvider({ info: () => undefined } as unknown as OperationalLog);
    let refreshes = 0;
    const subscription = provider.onDidChangeTreeData(() => { refreshes += 1; });
    try {
      provider.scheduleRefresh();
      provider.scheduleRefresh();
      await waitFor(() => refreshes === 1);
      provider.refresh();
      assert.equal(refreshes, 2);
    } finally {
      subscription.dispose();
      provider.dispose();
      provider.dispose();
    }
  });

  test('batches create, change and delete file-system events into current catalog data', async () => {
    const folder = vscode.workspace.workspaceFolders?.[0];
    assert.ok(folder);
    const uri = vscode.Uri.joinPath(folder.uri, 'docs', 'adr', '0002-watched.md');
    const renamedUri = vscode.Uri.joinPath(folder.uri, 'docs', 'adr', '0003-renamed.md');
    const provider = new AdrExplorerProvider({ info: () => undefined } as unknown as OperationalLog);
    let refreshes = 0;
    const subscription = provider.onDidChangeTreeData(() => { refreshes += 1; });
    try {
      await writeFile(uri.fsPath, '# Watched\n## Status\nProposed\n');
      await waitFor(() => refreshes > 0);
      assert.equal((await provider.getChildren()).filter((item) => item instanceof AdrNode).length, 2);
      const afterCreate = refreshes;
      await writeFile(uri.fsPath, '# Watched changed\n## Status\nAccepted\n');
      await waitFor(() => refreshes > afterCreate);
      const changed = (await provider.getChildren()).find((item) => item instanceof AdrNode
        && item.entry.fileName === '0002-watched.md');
      assert.ok(changed instanceof AdrNode);
      assert.equal(changed.description, 'Accepted');
      const afterChange = refreshes;
      await rename(uri.fsPath, renamedUri.fsPath);
      await waitFor(() => refreshes > afterChange);
      const afterRenameItems = (await provider.getChildren()).filter((item) => item instanceof AdrNode);
      assert.equal(afterRenameItems.some((item) => item.entry.fileName === '0002-watched.md'), false);
      assert.equal(afterRenameItems.some((item) => item.entry.fileName === '0003-renamed.md'), true);
      const afterRename = refreshes;
      await rm(renamedUri.fsPath);
      await waitFor(() => refreshes > afterRename);
      assert.equal((await provider.getChildren()).filter((item) => item instanceof AdrNode).length, 1);
    } finally {
      await rm(uri.fsPath, { force: true });
      await rm(renamedUri.fsPath, { force: true });
      subscription.dispose();
      provider.dispose();
    }
  });
});

async function waitFor(condition: () => boolean): Promise<void> {
  for (let attempt = 0; attempt < 40; attempt += 1) {
    if (condition()) return;
    await delay(100);
  }
  assert.fail('Timed out waiting for an ADR Explorer refresh event.');
}
