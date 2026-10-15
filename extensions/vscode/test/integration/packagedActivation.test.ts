import assert from 'node:assert/strict';
import * as vscode from 'vscode';

suite('Packaged ADR Guard extension', () => {
  test('installs and activates the versioned VSIX', async () => {
    const expectedVersion = process.env.ADR_GUARD_EXPECTED_EXTENSION_VERSION;
    assert.ok(expectedVersion, 'the smoke test must receive the expected extension version');
    const extension = vscode.extensions.all.find(
      (candidate) => candidate.packageJSON.name === 'adr-guard'
        && candidate.packageJSON.version === expectedVersion,
    );
    assert.ok(extension, 'the packaged extension must be installed in the isolated profile');
    assert.equal(extension.isActive, false);
    await extension.activate();
    assert.equal(extension.isActive, true);
    const commands = await vscode.commands.getCommands(true);
    assert.ok(commands.includes('adrGuard.checkInstallation'));
    assert.ok(commands.includes('adrGuard.validateAdrs'));
    assert.ok(commands.includes('adrGuard.refreshExplorer'));
    assert.ok(commands.includes('adrGuard.openVisual'));
  });
});
