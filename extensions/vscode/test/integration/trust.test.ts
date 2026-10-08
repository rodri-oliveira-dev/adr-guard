import assert from 'node:assert/strict';
import * as vscode from 'vscode';

suite('ADR Guard restricted workspace', () => {
  test('does not activate or run in an untrusted workspace', () => {
    assert.equal(vscode.workspace.isTrusted, false);
    const extension = vscode.extensions.all.find(
      (candidate) => candidate.packageJSON.name === 'adr-guard'
        && candidate.extensionPath.replaceAll('\\', '/').endsWith('/extensions/vscode'),
    );
    assert.ok(extension);
    assert.equal(extension.packageJSON.capabilities.untrustedWorkspaces.supported, false);
    assert.equal(extension.isActive, false);
  });
});
