import * as vscode from 'vscode';

export interface AdrGuardConfiguration {
  readonly executablePath?: string;
  readonly timeoutMilliseconds: number;
  readonly maxOutputBytes: number;
  readonly ignoredWorkspaceExecutablePath: boolean;
}

export interface ValidationConfiguration {
  readonly directory: string;
  readonly onSave: boolean;
  readonly debounceMilliseconds: number;
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

export function readValidationConfiguration(scope?: vscode.ConfigurationScope): ValidationConfiguration {
  const configuration = vscode.workspace.getConfiguration('adrGuard.validation', scope);
  return {
    directory: configuration.get<string>('directory', 'docs/adr').trim() || 'docs/adr',
    onSave: configuration.get<boolean>('onSave', false),
    debounceMilliseconds: clampInteger(
      configuration.get<number>('debounceMilliseconds', 750),
      200,
      5000,
    ),
  };
}

function clampInteger(value: number, minimum: number, maximum: number): number {
  if (!Number.isFinite(value)) {
    return minimum;
  }
  return Math.min(maximum, Math.max(minimum, Math.trunc(value)));
}
