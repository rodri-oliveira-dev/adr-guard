import * as vscode from 'vscode';
import { CliDiscovery, CliDiscoveryError } from '../cli/discovery';
import { CliRunner, type CliRunResult } from '../cli/runner';
import { readConfiguration } from '../configuration';
import { OperationalLog } from '../logging';
import { allWorkspaceRoots, asLocation, selectWorkspaceFolder } from '../workspace';
import { resolveWorkspaceRoot, WorkspacePolicyError } from '../workspacePolicy';

export interface CheckInstallationDependencies {
  readonly discovery: CliDiscovery;
  readonly runner: CliRunner;
  readonly log: OperationalLog;
}

export async function checkInstallation(dependencies: CheckInstallationDependencies): Promise<void> {
  try {
    const workspaceFolder = await selectWorkspaceFolder();
    if (workspaceFolder === undefined) {
      return;
    }
    const cwd = await resolveWorkspaceRoot(asLocation(workspaceFolder));
    const configuration = readConfiguration();
    if (configuration.ignoredWorkspaceExecutablePath) {
      dependencies.log.info('Ignored a workspace-scoped adrGuard.cli.path value; only the trusted user/machine value is accepted.');
    }

    const workspaceRoots = await allWorkspaceRoots();
    const executable = await dependencies.discovery.find({
      ...(configuration.executablePath ? { configuredPath: configuration.executablePath } : {}),
      workspaceRoots,
    });

    dependencies.log.info(`Checking ADR Guard CLI from ${executable.source} configuration.`);
    const result = await vscode.window.withProgress(
      {
        location: vscode.ProgressLocation.Notification,
        title: vscode.l10n.t('Checking ADR Guard installation'),
        cancellable: true,
      },
      async (_progress, token) => {
        const controller = new AbortController();
        const subscription = token.onCancellationRequested(() => controller.abort());
        try {
          return await dependencies.runner.run({
            executable: executable.path,
            args: ['--version'],
            cwd,
            timeoutMilliseconds: configuration.timeoutMilliseconds,
            maxOutputBytes: configuration.maxOutputBytes,
            signal: controller.signal,
          });
        } finally {
          subscription.dispose();
        }
      },
    );

    await reportResult(result, executable.source, dependencies.log);
  } catch (error: unknown) {
    const message = actionableError(error);
    dependencies.log.info(`Installation check failed: ${message}`);
    dependencies.log.show();
    await vscode.window.showErrorMessage(message);
  }
}

async function reportResult(
  result: CliRunResult,
  source: 'configured' | 'path',
  log: OperationalLog,
): Promise<void> {
  log.info(
    `CLI completed: termination=${result.termination}, exit=${result.exitCode ?? 'none'}, stdoutBytes=${Buffer.byteLength(result.stdout)}, stderrBytes=${Buffer.byteLength(result.stderr)}.`,
  );
  if (result.termination !== 'exited') {
    throw new Error(terminationMessage(result));
  }
  if (result.exitCode !== 0) {
    throw new Error(vscode.l10n.t(
      'ADR Guard failed with exit code {0}. See ADR Guard Output for details.',
      result.exitCode ?? 'unknown',
    ));
  }

  const version = firstSafeLine(result.stdout);
  if (version === undefined) {
    throw new Error(vscode.l10n.t('ADR Guard returned no recognizable version text.'));
  }
  log.info(`Detected ADR Guard version: ${version}`);
  await vscode.window.showInformationMessage(vscode.l10n.t('ADR Guard {0} is available ({1}).', version, source));
}

function firstSafeLine(output: string): string | undefined {
  const line = output.split(/\r?\n/u, 1)[0]?.trim();
  if (!line || line.length > 256 || containsUnsafeControlCharacter(line)) {
    return undefined;
  }
  return line;
}

function containsUnsafeControlCharacter(value: string): boolean {
  for (let index = 0; index < value.length; index += 1) {
    const codeUnit = value.charCodeAt(index);
    if ((codeUnit < 32 && codeUnit !== 9 && codeUnit !== 10 && codeUnit !== 13) || codeUnit === 127) {
      return true;
    }
  }
  return false;
}

function terminationMessage(result: CliRunResult): string {
  switch (result.termination) {
    case 'cancelled':
      return vscode.l10n.t('The ADR Guard installation check was cancelled.');
    case 'timed-out':
      return vscode.l10n.t('ADR Guard operation timed out. Adjust adrGuard.cli.timeoutMilliseconds if needed.');
    case 'output-limit':
      return vscode.l10n.t('ADR Guard exceeded the configured output limit and was stopped.');
    case 'spawn-error':
      return vscode.l10n.t('ADR Guard could not be started.');
    case 'exited':
      return vscode.l10n.t('ADR Guard exited unexpectedly.');
  }
}

function actionableError(error: unknown): string {
  if (error instanceof WorkspacePolicyError) {
    switch (error.code) {
      case 'untrusted': return vscode.l10n.t('Trust this workspace before running the ADR Guard CLI.');
      case 'missing': return vscode.l10n.t('Open a local folder or workspace before running ADR Guard.');
      case 'non-file': return vscode.l10n.t('ADR Guard requires a local file-system workspace; virtual workspace resources are unsupported.');
      default: return error.message;
    }
  }
  if (error instanceof CliDiscoveryError || error instanceof Error) {
    return error.message;
  }
  return vscode.l10n.t('ADR Guard installation check failed for an unknown reason.');
}
