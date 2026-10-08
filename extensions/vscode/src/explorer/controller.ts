import * as vscode from 'vscode';
import { ensureResourceWithinWorkspace, resolveWorkspaceRoot } from '../workspacePolicy';
import { asLocation } from '../workspace';
import { OperationalLog } from '../logging';
import { AdrExplorerProvider } from './provider';

export class AdrExplorerController implements vscode.Disposable {
  public readonly provider: AdrExplorerProvider;
  private readonly view: vscode.TreeView<ReturnTypeNode>;
  private readonly disposables: vscode.Disposable[];

  public constructor(log: OperationalLog) {
    this.provider = new AdrExplorerProvider(log);
    this.view = vscode.window.createTreeView('adrGuard.adrExplorer', {
      treeDataProvider: this.provider,
      showCollapseAll: true,
    });
    this.disposables = [this.provider, this.view];
  }

  public refresh(): void {
    this.provider.refresh();
  }

  public async open(value: unknown): Promise<void> {
    const uri = value instanceof vscode.Uri
      ? value
      : typeof value === 'object' && value !== null && 'resourceUri' in value
        && value.resourceUri instanceof vscode.Uri
        ? value.resourceUri
        : undefined;
    if (uri?.scheme !== 'file') return;
    const folder = vscode.workspace.getWorkspaceFolder(uri);
    if (folder?.uri.scheme !== 'file') return;
    const root = await resolveWorkspaceRoot(asLocation(folder));
    const safe = await ensureResourceWithinWorkspace(uri.fsPath, root);
    await vscode.window.showTextDocument(vscode.Uri.file(safe), { preview: true });
  }

  public async revealCurrent(): Promise<void> {
    const uri = vscode.window.activeTextEditor?.document.uri;
    if (uri === undefined) return;
    const node = await this.provider.findByUri(uri);
    if (node === undefined) {
      await vscode.window.showInformationMessage(vscode.l10n.t('The active file is not an ADR in the configured directory.'));
      return;
    }
    await this.view.reveal(node, { focus: true, select: true, expand: true });
  }

  public dispose(): void {
    for (const disposable of this.disposables) disposable.dispose();
  }
}

type ReturnTypeNode = Awaited<ReturnType<AdrExplorerProvider['getChildren']>>[number];
