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
      'onCommand:adrGuard.openVisual',
      'onCommand:adrGuard.createAdrWithForm',
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
      'adrGuard.openVisual',
      'adrGuard.createAdrWithForm',
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
    assert.equal(configuration.get<string>('baseReference'), 'main');
    assert.equal(vscode.workspace.getConfiguration('adrGuard.ui').get<string>('language'), 'auto');
    const creation = vscode.workspace.getConfiguration('adrGuard.creation');
    assert.equal(creation.get<string>('documentLanguage'), 'en-US');
    assert.equal(creation.get<string>('defaultTemplate'), 'configured');
    const cli = vscode.workspace.getConfiguration('adrGuard.cli');
    assert.equal(cli.get<string>('path'), '');
    assert.equal(cli.get<number>('timeoutMilliseconds'), 15000);
    assert.equal(cli.get<number>('maxOutputBytes'), 262144);
    assert.deepEqual(extension.packageJSON.contributes.viewsContainers.activitybar, [{
      id: 'adrGuard', title: 'ADR Guard', icon: 'images/icon-activity.svg',
    }]);
    assert.equal(extension.packageJSON.contributes.views.adrGuard[0].id, 'adrGuard.adrExplorer');

    const contributedCommands = extension.packageJSON.contributes.commands as {
      command: string;
      title: string;
      category: string;
      icon?: string;
    }[];
    const viewTitleCommands = extension.packageJSON.contributes.menus['view/title'] as {
      command: string;
      group: string;
      when: string;
    }[];
    assert.ok(contributedCommands.some((command) => command.command === 'adrGuard.selectAdrFormat'));
    const openVisualCommand = contributedCommands.find((command) => command.command === 'adrGuard.openVisual');
    assert.ok(openVisualCommand);
    assert.equal(openVisualCommand.icon, '$(list-flat)');
    assert.ok(openVisualCommand.title.length > 0);
    assert.ok(openVisualCommand.category.length > 0);
    assert.equal(viewTitleCommands.some((item) => item.command === 'adrGuard.selectAdrFormat'), false);
    assert.equal(viewTitleCommands.filter((item) => item.command === 'adrGuard.openVisual').length, 1);
    assert.deepEqual(viewTitleCommands.map((item) => item.command), [
      'adrGuard.createAdrWithForm',
      'adrGuard.refreshExplorer',
      'adrGuard.revealCurrentAdr',
      'adrGuard.openVisual',
    ]);
    assert.deepEqual(viewTitleCommands.at(-1), {
      command: 'adrGuard.openVisual',
      when: 'view == adrGuard.adrExplorer',
      group: 'navigation',
    });
  });

  test('reuses the visual ADR Guard panel', async () => {
    const countWebviews = (): number => vscode.window.tabGroups.all.flatMap((group) => group.tabs)
      .filter((tab) => tab.input instanceof vscode.TabInputWebview && tab.label === 'ADR Guard').length;
    assert.equal(countWebviews(), 0);
    await vscode.commands.executeCommand('adrGuard.openVisual');
    await waitFor(() => countWebviews() === 1);
    assert.equal(countWebviews(), 1);
    await vscode.commands.executeCommand('adrGuard.openVisual');
    assert.equal(countWebviews(), 1);
    await vscode.commands.executeCommand('workbench.action.closeActiveEditor');
  });

  test('opens the visual ADR list from the Activity Bar container', async () => {
    const countWebviews = (): number => vscode.window.tabGroups.all.flatMap((group) => group.tabs)
      .filter((tab) => tab.input instanceof vscode.TabInputWebview && tab.label === 'ADR Guard').length;
    assert.equal(countWebviews(), 0);
    await vscode.commands.executeCommand('workbench.view.extension.adrGuard');
    await waitFor(() => countWebviews() === 1);
    assert.equal(countWebviews(), 1);
    await vscode.commands.executeCommand('workbench.action.closeActiveEditor');
  });
});

async function waitFor(condition: () => boolean): Promise<void> {
  const deadline = Date.now() + 3000;
  while (Date.now() < deadline) {
    if (condition()) return;
    await new Promise((resolve) => setTimeout(resolve, 25));
  }
}
