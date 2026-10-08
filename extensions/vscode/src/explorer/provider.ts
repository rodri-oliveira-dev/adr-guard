import * as vscode from 'vscode';
import path from 'node:path';
import { readValidationConfiguration } from '../configuration';
import { OperationalLog } from '../logging';
import { asLocation } from '../workspace';
import {
  ensureResourceWithinWorkspace,
  isResolvedPathWithin,
  resolveWorkspaceRelativePath,
  resolveWorkspaceRoot,
} from '../workspacePolicy';
import { loadAdrCatalog, type AdrCatalogEntry, type AdrCatalogRelationship } from './catalog';
import { displayId, type AdrRelationshipKind } from './metadata';

type ExplorerNode = WorkspaceNode | AdrNode | RelationshipNode | MessageNode;

export class AdrExplorerProvider implements vscode.TreeDataProvider<ExplorerNode>, vscode.Disposable {
  private readonly changed = new vscode.EventEmitter<ExplorerNode | undefined>();
  private readonly disposables: vscode.Disposable[] = [this.changed];
  private readonly watchers: vscode.FileSystemWatcher[] = [];
  private readonly cache = new Map<string, readonly AdrCatalogEntry[]>();
  private readonly pending = new Map<string, {
    readonly generation: number;
    readonly promise: Promise<readonly AdrCatalogEntry[]>;
  }>();
  private generation = 0;
  private refreshTimer: NodeJS.Timeout | undefined;
  private disposed = false;
  public readonly onDidChangeTreeData = this.changed.event;

  public constructor(private readonly log: OperationalLog) {
    this.rebuildWatchers();
    this.disposables.push(
      vscode.workspace.onDidChangeWorkspaceFolders(() => {
        this.rebuildWatchers();
        this.refresh();
      }),
      vscode.workspace.onDidChangeConfiguration((event) => {
        if (event.affectsConfiguration('adrGuard.validation.directory')
          || event.affectsConfiguration('adrGuard.validation.adrFormat')) {
          this.rebuildWatchers();
          this.refresh();
        }
      }),
    );
  }

  public getTreeItem(element: ExplorerNode): vscode.TreeItem {
    return element;
  }

  public async getChildren(element?: ExplorerNode): Promise<ExplorerNode[]> {
    if (!vscode.workspace.isTrusted) return [new MessageNode(vscode.l10n.t('Trust this workspace to browse ADRs.'))];
    const folders = (vscode.workspace.workspaceFolders ?? []).filter((folder) => folder.uri.scheme === 'file');
    if (folders.length === 0) return [new MessageNode(vscode.l10n.t('Open a local workspace folder to browse ADRs.'))];
    if (element === undefined) {
      if (folders.length > 1) return folders.map((folder) => new WorkspaceNode(folder));
      const folder = folders[0];
      return folder === undefined ? [] : this.folderChildren(folder);
    }
    if (element instanceof WorkspaceNode) return this.folderChildren(element.folder);
    if (element instanceof AdrNode) {
      return element.entry.relationships.map((relationship) => new RelationshipNode(relationship, element));
    }
    return [];
  }

  public getParent(element: ExplorerNode): ExplorerNode | undefined {
    if (element instanceof RelationshipNode) return element.parent;
    if (element instanceof AdrNode && (vscode.workspace.workspaceFolders?.length ?? 0) > 1) {
      const folder = vscode.workspace.getWorkspaceFolder(vscode.Uri.file(element.entry.filePath));
      return folder === undefined ? undefined : new WorkspaceNode(folder);
    }
    return undefined;
  }

  public refresh(): void {
    if (this.disposed) return;
    this.generation += 1;
    this.cache.clear();
    this.changed.fire(undefined);
  }

  public scheduleRefresh(): void {
    if (this.disposed) return;
    if (this.refreshTimer !== undefined) clearTimeout(this.refreshTimer);
    this.refreshTimer = setTimeout(() => {
      this.refreshTimer = undefined;
      this.refresh();
    }, 200);
  }

  public async findByUri(uri: vscode.Uri): Promise<AdrNode | undefined> {
    if (uri.scheme !== 'file') return undefined;
    const folder = vscode.workspace.getWorkspaceFolder(uri);
    if (folder?.uri.scheme !== 'file') return undefined;
    const target = await ensureResourceWithinWorkspace(uri.fsPath, await resolveWorkspaceRoot(asLocation(folder)));
    const entries = await this.catalog(folder);
    const entry = entries.find((candidate) => samePath(candidate.filePath, target));
    return entry === undefined ? undefined : new AdrNode(entry);
  }

  public dispose(): void {
    if (this.disposed) return;
    this.disposed = true;
    this.generation += 1;
    if (this.refreshTimer !== undefined) clearTimeout(this.refreshTimer);
    this.disposeWatchers();
    for (const disposable of this.disposables) disposable.dispose();
    this.cache.clear();
    this.pending.clear();
  }

  private async folderChildren(folder: vscode.WorkspaceFolder): Promise<ExplorerNode[]> {
    try {
      const entries = await this.catalog(folder);
      return entries.length === 0
        ? [new MessageNode(vscode.l10n.t('No ADR Markdown files were found in the configured directory.'))]
        : entries.map((entry) => new AdrNode(entry));
    } catch (error: unknown) {
      this.log.info(`ADR Explorer could not load workspace '${folder.name}': ${safeError(error)}`);
      return [new MessageNode(vscode.l10n.t('The configured ADR directory is unavailable or unsafe.'))];
    }
  }

  private async catalog(folder: vscode.WorkspaceFolder): Promise<readonly AdrCatalogEntry[]> {
    if (this.disposed) return [];
    const key = folder.uri.toString();
    const cached = this.cache.get(key);
    if (cached !== undefined) return cached;
    let active = this.pending.get(key);
    if (active === undefined) {
      const requestedGeneration = this.generation;
      const promise = this.loadCatalog(folder);
      active = { generation: requestedGeneration, promise };
      this.pending.set(key, active);
    }
    let entries: readonly AdrCatalogEntry[];
    try {
      entries = await active.promise;
    } finally {
      if (this.pending.get(key) === active) this.pending.delete(key);
    }
    if (active.generation !== this.generation) return this.catalog(folder);
    this.cache.set(key, entries);
    return entries;
  }

  private async loadCatalog(folder: vscode.WorkspaceFolder): Promise<readonly AdrCatalogEntry[]> {
    const root = await resolveWorkspaceRoot(asLocation(folder));
    const configuration = readValidationConfiguration(folder.uri);
    const directory = await ensureResourceWithinWorkspace(
      resolveWorkspaceRelativePath(root, configuration.directory),
      root,
    );
    if (!isResolvedPathWithin(directory, root)) throw new Error('ADR directory is outside workspace');
    return loadAdrCatalog(root, directory, configuration.adrFormat);
  }

  private rebuildWatchers(): void {
    this.disposeWatchers();
    for (const folder of vscode.workspace.workspaceFolders ?? []) {
      if (folder.uri.scheme !== 'file') continue;
      const directory = readValidationConfiguration(folder.uri).directory.replaceAll('\\', '/');
      if (directory.length === 0 || directory.startsWith('/') || directory.split('/').includes('..')) continue;
      const watcher = vscode.workspace.createFileSystemWatcher(
        new vscode.RelativePattern(folder, `${directory}/**/*.md`),
      );
      watcher.onDidCreate(() => this.scheduleRefresh());
      watcher.onDidChange(() => this.scheduleRefresh());
      watcher.onDidDelete(() => this.scheduleRefresh());
      this.watchers.push(watcher);
    }
  }

  private disposeWatchers(): void {
    for (const watcher of this.watchers.splice(0)) watcher.dispose();
  }
}

export class WorkspaceNode extends vscode.TreeItem {
  public constructor(public readonly folder: vscode.WorkspaceFolder) {
    super(folder.name, vscode.TreeItemCollapsibleState.Expanded);
    this.contextValue = 'adrWorkspace';
    this.iconPath = new vscode.ThemeIcon('root-folder');
    this.id = `workspace:${folder.uri.toString()}`;
  }
}

export class AdrNode extends vscode.TreeItem {
  public constructor(public readonly entry: AdrCatalogEntry) {
    const id = displayId(entry.fileName, entry.metadata.id);
    super(`${id} — ${entry.metadata.title ?? vscode.l10n.t('Unknown title')}`,
      entry.relationships.length === 0
        ? vscode.TreeItemCollapsibleState.None
        : vscode.TreeItemCollapsibleState.Collapsed);
    this.description = entry.metadata.status ?? vscode.l10n.t('Unknown status');
    this.tooltip = vscode.l10n.t(
      '{0}\nStatus: {1}\nDisplay metadata only; validation remains the responsibility of the ADR Guard CLI.',
      entry.relativePath,
      this.description,
    );
    this.resourceUri = vscode.Uri.file(entry.filePath);
    this.iconPath = new vscode.ThemeIcon('file');
    this.contextValue = 'adrFile';
    this.id = `adr:${this.resourceUri.toString()}`;
    this.command = {
      command: 'adrGuard.openAdr',
      title: vscode.l10n.t('Open ADR'),
      arguments: [this.resourceUri],
    };
  }
}

export class RelationshipNode extends vscode.TreeItem {
  public constructor(
    public readonly relationship: AdrCatalogRelationship,
    public readonly parent: AdrNode,
  ) {
    super(`${relationshipLabel(relationship.kind)}: ${relationship.label}`, vscode.TreeItemCollapsibleState.None);
    this.resourceUri = vscode.Uri.file(relationship.targetPath);
    this.iconPath = new vscode.ThemeIcon('references');
    this.contextValue = 'adrRelationship';
    this.id = `relationship:${parent.entry.filePath}:${relationship.kind}:${relationship.targetPath}`;
    this.command = {
      command: 'adrGuard.openAdr',
      title: vscode.l10n.t('Open Related ADR'),
      arguments: [this.resourceUri],
    };
  }
}

class MessageNode extends vscode.TreeItem {
  public constructor(label: string) {
    super(label, vscode.TreeItemCollapsibleState.None);
    this.iconPath = new vscode.ThemeIcon('info');
    this.contextValue = 'adrExplorerMessage';
  }
}

function relationshipLabel(kind: AdrRelationshipKind): string {
  switch (kind) {
    case 'reference': return vscode.l10n.t('Reference');
    case 'superseded-by': return vscode.l10n.t('Superseded by');
    case 'supersedes': return vscode.l10n.t('Supersedes');
    case 'depends-on': return vscode.l10n.t('Depends on');
  }
}

function samePath(left: string, right: string): boolean {
  const normalize = (value: string): string => process.platform === 'win32'
    ? path.resolve(value).toLowerCase()
    : path.resolve(value);
  return normalize(left) === normalize(right);
}

function safeError(error: unknown): string {
  const message = error instanceof Error ? error.message : 'unknown error';
  return message.replace(/[\r\n\0]/gu, ' ').slice(0, 300);
}
