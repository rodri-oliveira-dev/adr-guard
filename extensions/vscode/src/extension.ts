import * as vscode from 'vscode';
import { CliDiscovery } from './cli/discovery';
import { CliRunner } from './cli/runner';
import { checkInstallation } from './commands/checkInstallation';
import { OperationalLog } from './logging';

let activeRunner: CliRunner | undefined;

export function activate(context: vscode.ExtensionContext): void {
  const runner = new CliRunner();
  const log = new OperationalLog();
  const discovery = new CliDiscovery();
  activeRunner = runner;

  context.subscriptions.push(
    runner,
    log,
    vscode.commands.registerCommand('adrGuard.checkInstallation', async () =>
      checkInstallation({ discovery, runner, log })),
  );
  log.info('ADR Guard activated on explicit command; no CLI process has been started.');
}

export function deactivate(): void {
  activeRunner?.dispose();
  activeRunner = undefined;
}
