import * as vscode from 'vscode';
import { CliDiscovery } from './cli/discovery';
import { CliRunner } from './cli/runner';
import { CliExecutor } from './cli/executor';
import { checkInstallation } from './commands/checkInstallation';
import {
  createAdr,
  generateIndex,
  initializeRepository,
  validateAdrs,
} from './commands/coreCommands';
import { OperationalLog } from './logging';
import { DiagnosticManager } from './validation/diagnostics';
import { SaveValidationController } from './validation/onSave';

let activeRunner: CliRunner | undefined;

export function activate(context: vscode.ExtensionContext): void {
  const runner = new CliRunner();
  const log = new OperationalLog();
  const discovery = new CliDiscovery();
  const executor = new CliExecutor(discovery, runner, log);
  const diagnostics = new DiagnosticManager(executor, log);
  const saveValidation = new SaveValidationController(diagnostics, log);
  const core = { executor, diagnostics, log };
  activeRunner = runner;

  context.subscriptions.push(
    runner,
    log,
    diagnostics,
    saveValidation,
    vscode.commands.registerCommand('adrGuard.checkInstallation', async () =>
      checkInstallation({ discovery, runner, log })),
    vscode.commands.registerCommand('adrGuard.initializeRepository', async () => initializeRepository(core)),
    vscode.commands.registerCommand('adrGuard.createAdr', async () => createAdr(core)),
    vscode.commands.registerCommand('adrGuard.validateAdrs', async () => validateAdrs(core)),
    vscode.commands.registerCommand('adrGuard.generateIndex', async () => generateIndex(core)),
  );
  log.info('ADR Guard activated; no CLI process has been started.');
}

export function deactivate(): void {
  activeRunner?.dispose();
  activeRunner = undefined;
}
