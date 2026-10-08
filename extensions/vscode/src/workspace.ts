import * as vscode from 'vscode';
import {
  eligibleWorkspaceFolders,
  preferredWorkspaceLocation,
  resolveWorkspaceRoot,
  type WorkspaceLocation,
} from './workspacePolicy';

export function asLocation(folder: vscode.WorkspaceFolder): WorkspaceLocation {
  return { name: folder.name, scheme: folder.uri.scheme, fsPath: folder.uri.fsPath };
}

export async function selectWorkspaceFolder(): Promise<vscode.WorkspaceFolder | undefined> {
  const eligible = eligibleWorkspaceFolders(
    vscode.workspace.isTrusted,
    vscode.workspace.workspaceFolders?.map(asLocation),
  );
  const active = vscode.window.activeTextEditor === undefined
    ? undefined
    : vscode.workspace.getWorkspaceFolder(vscode.window.activeTextEditor.document.uri);
  const preferred = preferredWorkspaceLocation(
    eligible,
    active?.uri.scheme === 'file' ? active.uri.fsPath : undefined,
  );
  if (preferred !== undefined) {
    return vscode.workspace.workspaceFolders?.find((folder) => folder.uri.fsPath === preferred.fsPath);
  }
  return vscode.window.showWorkspaceFolderPick({
    placeHolder: vscode.l10n.t('Select the workspace folder where ADR Guard should run'),
  });
}

export async function allWorkspaceRoots(): Promise<string[]> {
  return Promise.all(
    (vscode.workspace.workspaceFolders ?? [])
      .filter((folder) => folder.uri.scheme === 'file')
      .map(async (folder) => resolveWorkspaceRoot(asLocation(folder))),
  );
}
