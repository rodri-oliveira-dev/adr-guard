import * as vscode from 'vscode';
import { readConfiguration } from '../configuration';
import { OperationalLog } from '../logging';
import { allWorkspaceRoots, asLocation } from '../workspace';
import { resolveWorkspaceRoot } from '../workspacePolicy';
import { CliDiscovery } from './discovery';
import { CliRunner, type CliRunResult } from './runner';

export interface CliExecution {
  readonly result: CliRunResult;
  readonly workspaceRoot: string;
}

export class CliExecutor {
  public constructor(
    private readonly discovery: CliDiscovery,
    private readonly runner: CliRunner,
    private readonly log: OperationalLog,
  ) {}

  public async execute(
    folder: vscode.WorkspaceFolder,
    args: readonly string[],
    signal?: AbortSignal,
  ): Promise<CliExecution> {
    const workspaceRoot = await resolveWorkspaceRoot(asLocation(folder));
    const configuration = readConfiguration();
    if (configuration.ignoredWorkspaceExecutablePath) {
      this.log.info('Ignored a workspace-scoped adrGuard.cli.path value; only the trusted user/machine value is accepted.');
    }
    const executable = await this.discovery.find({
      ...(configuration.executablePath ? { configuredPath: configuration.executablePath } : {}),
      workspaceRoots: await allWorkspaceRoots(),
    });
    this.log.info(`Starting ADR Guard command '${args[0] ?? 'unknown'}' from ${executable.source} configuration.`);
    const result = await this.runner.run({
      executable: executable.path,
      args,
      cwd: workspaceRoot,
      timeoutMilliseconds: configuration.timeoutMilliseconds,
      maxOutputBytes: configuration.maxOutputBytes,
      ...(signal === undefined ? {} : { signal }),
    });
    this.log.info(
      `CLI completed: command=${args[0] ?? 'unknown'}, termination=${result.termination}, exit=${result.exitCode ?? 'none'}, stdoutBytes=${Buffer.byteLength(result.stdout)}, stderrBytes=${Buffer.byteLength(result.stderr)}.`,
    );
    return { result, workspaceRoot };
  }
}

export function requireSuccessfulResult(result: CliRunResult): void {
  if (result.termination !== 'exited') {
    switch (result.termination) {
      case 'cancelled':
        throw new Error(vscode.l10n.t('The operation was cancelled.'));
      case 'timed-out':
        throw new Error(vscode.l10n.t('ADR Guard operation timed out. Adjust adrGuard.cli.timeoutMilliseconds if needed.'));
      case 'output-limit':
        throw new Error(vscode.l10n.t('ADR Guard exceeded the configured output limit and was stopped.'));
      case 'spawn-error':
        throw new Error(vscode.l10n.t('ADR Guard could not be started.'));
    }
  }
  if (result.exitCode !== 0) {
    const detail = safeErrorLine(result.stderr);
    throw new Error(vscode.l10n.t(
      'ADR Guard failed with exit code {0}. {1}',
      result.exitCode ?? 'unknown',
      detail ?? vscode.l10n.t('See ADR Guard Output for details.'),
    ));
  }
}

export function safeErrorLine(stderr: string): string | undefined {
  const line = stderr.split(/\r?\n/u, 1)[0]?.trim();
  if (line === undefined || line.length === 0 || line.length > 512 || containsUnsafeControlCharacter(line)) {
    return undefined;
  }
  return line;
}

function containsUnsafeControlCharacter(value: string): boolean {
  for (let index = 0; index < value.length; index += 1) {
    const codeUnit = value.charCodeAt(index);
    if ((codeUnit < 32 && codeUnit !== 9) || codeUnit === 127) return true;
  }
  return false;
}

export async function withCancellationProgress<T>(
  title: string,
  operation: (signal: AbortSignal) => Promise<T>,
): Promise<T> {
  return vscode.window.withProgress(
    { location: vscode.ProgressLocation.Notification, title, cancellable: true },
    async (_progress, token) => {
      const controller = new AbortController();
      const subscription = token.onCancellationRequested(() => controller.abort());
      try {
        return await operation(controller.signal);
      } finally {
        subscription.dispose();
      }
    },
  );
}
