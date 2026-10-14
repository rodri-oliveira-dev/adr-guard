import * as vscode from 'vscode';
import { CliDiscovery } from './cli/discovery';
import { CliRunner } from './cli/runner';
import { CliExecutor } from './cli/executor';
import { checkInstallation } from './commands/checkInstallation';
import {
  createAdr,
  generateIndex,
  initializeRepository,
  selectAdrFormat,
  validateAdrs,
  validateChangedAdrs,
  validateWithBaseline,
} from './commands/coreCommands';
import { AdrExplorerController } from './explorer/controller';
import { OperationalLog } from './logging';
import { DiagnosticManager } from './validation/diagnostics';
import { SaveValidationController } from './validation/onSave';
import { NewAdrFormPanel } from './commands/newAdrForm';

let activeRunner: CliRunner | undefined;

export function activate(context: vscode.ExtensionContext): void {
  const runner = new CliRunner();
  const log = new OperationalLog();
  const discovery = new CliDiscovery();
  const executor = new CliExecutor(discovery, runner, log);
  const diagnostics = new DiagnosticManager(executor, log);
  const saveValidation = new SaveValidationController(diagnostics, log);
  const core = { executor, diagnostics, log };
  const installation = { discovery, runner, log };
  const explorer = new AdrExplorerController(log, () => { void openVisual('list'); });
  activeRunner = runner;

  async function openVisual(screen: 'list' | 'form'): Promise<void> {
    await NewAdrFormPanel.show({
      executor,
      log,
      extensionUri: context.extensionUri,
      explorer: explorer.provider,
      refreshExplorer: () => explorer.refresh(),
      initializeRepository: async (folder) => initializeRepository(core, folder),
      checkInstallation: async (folder, showNotification) => checkInstallation(installation, folder, showNotification),
    }, screen);
  }

  context.subscriptions.push(
    runner,
    log,
    diagnostics,
    saveValidation,
    explorer,
    vscode.commands.registerCommand('adrGuard.checkInstallation', async () =>
      checkInstallation(installation)),
    vscode.commands.registerCommand('adrGuard.initializeRepository', async () => initializeRepository(core)),
    vscode.commands.registerCommand('adrGuard.createAdr', async () => createAdr(core)),
    vscode.commands.registerCommand('adrGuard.openVisual', async () => openVisual('list')),
    vscode.commands.registerCommand('adrGuard.createAdrWithForm', async () => openVisual('form')),
    vscode.commands.registerCommand('adrGuard.validateAdrs', async () => validateAdrs(core)),
    vscode.commands.registerCommand('adrGuard.validateChangedAdrs', async () => validateChangedAdrs(core)),
    vscode.commands.registerCommand('adrGuard.validateWithBaseline', async () => validateWithBaseline(core)),
    vscode.commands.registerCommand('adrGuard.generateIndex', async () => generateIndex(core)),
    vscode.commands.registerCommand('adrGuard.selectAdrFormat', async () => selectAdrFormat()),
    vscode.commands.registerCommand('adrGuard.refreshExplorer', () => explorer.refresh()),
    vscode.commands.registerCommand('adrGuard.openAdr', async (uri: unknown) => explorer.open(uri)),
    vscode.commands.registerCommand('adrGuard.revealCurrentAdr', async () => explorer.revealCurrent()),
  );
  log.info('ADR Guard activated; no CLI process has been started.');
}

export function deactivate(): void {
  activeRunner?.dispose();
  activeRunner = undefined;
}
