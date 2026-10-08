import assert from 'node:assert/strict';
import * as vscode from 'vscode';

suite('ADR Guard extension activation', () => {
  test('stays inactive at startup and registers the command on explicit activation', async () => {
    const extension = vscode.extensions.all.find(
      (candidate) => candidate.packageJSON.name === 'adr-guard'
        && candidate.extensionPath.replaceAll('\\', '/').endsWith('/extensions/vscode'),
    );
    assert.ok(extension, 'extension must be discoverable in the development host');
    const activationEvents = extension.packageJSON.activationEvents as string[];
    assert.deepEqual(activationEvents, [
      'onCommand:adrGuard.checkInstallation',
      'onCommand:adrGuard.initializeRepository',
      'onCommand:adrGuard.createAdr',
      'onCommand:adrGuard.validateAdrs',
      'onCommand:adrGuard.validateChangedAdrs',
      'onCommand:adrGuard.validateWithBaseline',
      'onCommand:adrGuard.generateIndex',
      'onCommand:adrGuard.selectAdrFormat',
      'onCommand:adrGuard.refreshExplorer',
      'onCommand:adrGuard.openAdr',
      'onCommand:adrGuard.revealCurrentAdr',
      'onView:adrGuard.adrExplorer',
    ]);
    assert.equal(activationEvents.includes('*'), false);
    assert.equal(activationEvents.includes('onStartupFinished'), false);
    assert.deepEqual(extension.packageJSON.extensionKind, ['workspace']);
    assert.equal(extension.packageJSON.capabilities.untrustedWorkspaces.supported, false);
    assert.equal(extension.packageJSON.capabilities.virtualWorkspaces.supported, false);

    await extension.activate();
    assert.equal(extension.isActive, true);
    const commands = await vscode.commands.getCommands(true);
    for (const command of [
      'adrGuard.checkInstallation',
      'adrGuard.initializeRepository',
      'adrGuard.createAdr',
      'adrGuard.validateAdrs',
      'adrGuard.validateChangedAdrs',
      'adrGuard.validateWithBaseline',
      'adrGuard.generateIndex',
      'adrGuard.selectAdrFormat',
      'adrGuard.refreshExplorer',
      'adrGuard.openAdr',
      'adrGuard.revealCurrentAdr',
    ]) {
      assert.ok(commands.includes(command), `${command} must be registered`);
    }
    const configuration = vscode.workspace.getConfiguration('adrGuard.validation');
    assert.equal(configuration.get<boolean>('onSave'), false);
    assert.equal(configuration.get<number>('debounceMilliseconds'), 750);
    assert.equal(configuration.get<string>('adrFormat'), 'canonical');
    assert.equal(configuration.get<string>('baseline'), '.adrguard-baseline.json');
    assert.equal(extension.packageJSON.contributes.views.explorer[0].id, 'adrGuard.adrExplorer');
  });
});
