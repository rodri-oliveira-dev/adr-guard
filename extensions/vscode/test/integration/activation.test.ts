import assert from 'node:assert/strict';
import * as vscode from 'vscode';

suite('ADR Guard extension activation', () => {
  test('stays inactive at startup and registers the command on explicit activation', async () => {
    const extension = vscode.extensions.all.find(
      (candidate) => candidate.packageJSON.name === 'adr-guard'
        && candidate.extensionPath.replaceAll('\\', '/').endsWith('/extensions/vscode'),
    );
    assert.ok(extension, 'extension must be discoverable in the development host');
    assert.equal(extension.isActive, false, 'activation must not occur at startup');

    assert.deepEqual(extension.packageJSON.activationEvents, ['onCommand:adrGuard.checkInstallation']);
    assert.deepEqual(extension.packageJSON.extensionKind, ['workspace']);
    assert.equal(extension.packageJSON.capabilities.untrustedWorkspaces.supported, false);
    assert.equal(extension.packageJSON.capabilities.virtualWorkspaces.supported, false);

    await extension.activate();
    assert.equal(extension.isActive, true);
    const commands = await vscode.commands.getCommands(true);
    assert.ok(commands.includes('adrGuard.checkInstallation'));
  });
});
