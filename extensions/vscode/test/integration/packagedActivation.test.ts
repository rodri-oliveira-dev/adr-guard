import assert from 'node:assert/strict';
import * as vscode from 'vscode';

suite('Packaged ADR Guard extension', () => {
  test('installs and activates the 0.1.0 VSIX', async () => {
    const extension = vscode.extensions.all.find(
      (candidate) => candidate.packageJSON.name === 'adr-guard'
        && candidate.packageJSON.version === '0.1.0',
    );
    assert.ok(extension, 'the packaged extension must be installed in the isolated profile');
    assert.equal(extension.isActive, false);
    await extension.activate();
    assert.equal(extension.isActive, true);
    const commands = await vscode.commands.getCommands(true);
    assert.ok(commands.includes('adrGuard.checkInstallation'));
    assert.ok(commands.includes('adrGuard.validateAdrs'));
    assert.ok(commands.includes('adrGuard.refreshExplorer'));
  });
});
