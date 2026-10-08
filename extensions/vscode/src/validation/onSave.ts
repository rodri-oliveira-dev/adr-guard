import { realpath } from 'node:fs/promises';
import path from 'node:path';
import * as vscode from 'vscode';
import { readValidationConfiguration } from '../configuration';
import { OperationalLog } from '../logging';
import { asLocation } from '../workspace';
import { isResolvedPathWithin, resolveWorkspaceRelativePath, resolveWorkspaceRoot } from '../workspacePolicy';
import { DiagnosticManager } from './diagnostics';
import { ValidationScheduler } from './scheduler';

export class SaveValidationController implements vscode.Disposable {
  private readonly schedulers = new Map<string, ValidationScheduler>();
  private readonly subscriptions: vscode.Disposable[];

  public constructor(
    private readonly diagnostics: DiagnosticManager,
    private readonly log: OperationalLog,
  ) {
    this.subscriptions = [
      vscode.workspace.onDidSaveTextDocument((document) => { void this.saved(document); }),
      vscode.workspace.onDidDeleteFiles((event) => event.files.forEach((uri) => this.diagnostics.delete(uri))),
      vscode.workspace.onDidRenameFiles((event) => event.files.forEach(({ oldUri }) => this.diagnostics.delete(oldUri))),
      vscode.workspace.onDidChangeWorkspaceFolders((event) => {
        for (const folder of event.removed) void this.clearFolder(folder);
      }),
      vscode.workspace.onDidChangeConfiguration((event) => {
        if (event.affectsConfiguration('adrGuard.validation')) {
          this.cancelAll();
          void this.clearAllWorkspaceDiagnostics();
        }
      }),
    ];
  }

  public dispose(): void {
    this.cancelAll();
    for (const subscription of this.subscriptions) subscription.dispose();
  }

  private async saved(document: vscode.TextDocument): Promise<void> {
    if (!vscode.workspace.isTrusted || document.uri.scheme !== 'file' || document.isDirty) return;
    if (path.extname(document.uri.fsPath).toLowerCase() !== '.md' || path.basename(document.uri.fsPath).toLowerCase() === 'readme.md') return;
    const folder = vscode.workspace.getWorkspaceFolder(document.uri);
    if (folder?.uri.scheme !== 'file') return;
    const configuration = readValidationConfiguration(folder.uri);
    if (!configuration.onSave) return;
    try {
      const root = await resolveWorkspaceRoot(asLocation(folder));
      const [savedPath, adrDirectory] = await Promise.all([
        realpath(document.uri.fsPath),
        realpath(resolveWorkspaceRelativePath(root, configuration.directory)),
      ]);
      if (!isResolvedPathWithin(savedPath, adrDirectory)) return;
      let scheduler = this.schedulers.get(root);
      if (scheduler === undefined) {
        scheduler = new ValidationScheduler(
          async (signal) => {
            await this.diagnostics.validate(
              folder,
              configuration.directory,
              signal,
              { adrFormat: configuration.adrFormat },
            );
          },
          (error) => this.log.info(`Automatic validation failed: ${error instanceof Error ? error.message : 'unknown error'}`),
        );
        this.schedulers.set(root, scheduler);
      }
      scheduler.schedule(configuration.debounceMilliseconds);
    } catch (error: unknown) {
      this.log.info(`Skipped automatic validation: ${error instanceof Error ? error.message : 'unknown error'}`);
    }
  }

  private async clearFolder(folder: vscode.WorkspaceFolder): Promise<void> {
    try {
      const root = await resolveWorkspaceRoot(asLocation(folder));
      this.schedulers.get(root)?.dispose();
      this.schedulers.delete(root);
      this.diagnostics.clearWorkspace(root);
    } catch {
      // An inaccessible removed workspace has no safe diagnostics target to retain.
    }
  }

  private cancelAll(): void {
    for (const scheduler of this.schedulers.values()) scheduler.dispose();
    this.schedulers.clear();
  }

  private async clearAllWorkspaceDiagnostics(): Promise<void> {
    for (const folder of vscode.workspace.workspaceFolders ?? []) {
      if (folder.uri.scheme !== 'file') continue;
      try {
        this.diagnostics.clearWorkspace(await resolveWorkspaceRoot(asLocation(folder)));
      } catch {
        // Inaccessible workspaces cannot own a valid local diagnostic URI.
      }
    }
  }
}
