import * as vscode from 'vscode';

export interface AdrGuardConfiguration {
  readonly executablePath?: string;
  readonly timeoutMilliseconds: number;
  readonly maxOutputBytes: number;
  readonly ignoredWorkspaceExecutablePath: boolean;
}

export function readConfiguration(): AdrGuardConfiguration {
  const configuration = vscode.workspace.getConfiguration('adrGuard.cli');
  const pathInspection = configuration.inspect<string>('path');
  const executablePath = pathInspection?.globalValue?.trim();
  const ignoredWorkspaceExecutablePath =
    pathInspection?.workspaceValue !== undefined || pathInspection?.workspaceFolderValue !== undefined;

  return {
    ...(executablePath ? { executablePath } : {}),
    timeoutMilliseconds: clampInteger(
      configuration.get<number>('timeoutMilliseconds', 15000),
      1000,
      120000,
    ),
    maxOutputBytes: clampInteger(
      configuration.get<number>('maxOutputBytes', 262144),
      1024,
      1048576,
    ),
    ignoredWorkspaceExecutablePath,
  };
}

function clampInteger(value: number, minimum: number, maximum: number): number {
  if (!Number.isFinite(value)) {
    return minimum;
  }
  return Math.min(maximum, Math.max(minimum, Math.trunc(value)));
}
