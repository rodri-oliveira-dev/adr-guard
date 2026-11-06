import { randomBytes } from 'node:crypto';
import * as vscode from 'vscode';
import { CliDiscoveryError } from '../cli/discovery';
import { CliExecutor, requireSuccessfulResult } from '../cli/executor';
import { readValidationConfiguration } from '../configuration';
import { parseRelationshipGraphReport, RelationshipGraphContractError, RelationshipGraphReportLimitError, type RelationshipGraphReport } from '../contracts/relationshipGraph';
import { OperationalLog } from '../logging';
import { asLocation, selectWorkspaceFolder } from '../workspace';
import { relationshipGraphWebviewHtml } from '../webview/relationshipGraphHtml';
import { parseRelationshipGraphMessage } from '../webview/relationshipGraphMessages';
import { layoutRelationshipGraph, maximumVisibleRelationshipGraphNodes, relationshipGraphStatuses, type RelationshipGraphFilters } from '../webview/relationshipGraphModel';
import { ensureExistingMarkdownFileWithinWorkspace, ensureResourceWithinWorkspace, resolveWorkspaceRelativePath, resolveWorkspaceRoot, WorkspacePolicyError } from '../workspacePolicy';

interface RelationshipGraphDependencies { readonly executor: CliExecutor; readonly log: OperationalLog; readonly extensionUri: vscode.Uri }

class IncompatibleRelationshipsCliError extends Error {
  public constructor() { super('The installed ADR Guard CLI does not support relationship graphs.'); this.name = 'IncompatibleRelationshipsCliError'; }
}

export class RelationshipGraphPanel implements vscode.Disposable {
  private static current: RelationshipGraphPanel | undefined;
  private readonly panel: vscode.WebviewPanel;
  private readonly disposables: vscode.Disposable[] = [];
  private active: AbortController | undefined;
  private disposed = false;
  private report: RelationshipGraphReport | undefined;
  private filters: RelationshipGraphFilters = { status: null, type: null, focusPath: null };

  private constructor(private readonly dependencies: RelationshipGraphDependencies, private folder: vscode.WorkspaceFolder) {
    this.panel = vscode.window.createWebviewPanel('adrGuard.relationshipGraph', vscode.l10n.t('ADR Relationships'), vscode.ViewColumn.Active, {
      enableScripts: true,
      retainContextWhenHidden: true,
      localResourceRoots: [vscode.Uri.joinPath(dependencies.extensionUri, 'media')],
    });
    const nonce = randomBytes(16).toString('base64');
    this.panel.webview.html = relationshipGraphWebviewHtml(
      this.panel.webview.cspSource,
      this.panel.webview.asWebviewUri(vscode.Uri.joinPath(dependencies.extensionUri, 'media', 'relationshipGraph.js')).toString(),
      this.panel.webview.asWebviewUri(vscode.Uri.joinPath(dependencies.extensionUri, 'media', 'relationshipGraph.css')).toString(),
      vscode.env.language,
      nonce,
    );
    this.disposables.push(this.panel, this.panel.onDidDispose(() => this.dispose()), this.panel.webview.onDidReceiveMessage((message: unknown) => this.receive(message)));
  }

  public static async show(dependencies: RelationshipGraphDependencies): Promise<void> {
    const folder = await selectWorkspaceFolder();
    if (folder === undefined) return;
    if (this.current !== undefined && !this.current.disposed) {
      this.current.folder = folder;
      this.current.panel.reveal(vscode.ViewColumn.Active);
      await this.current.load();
      return;
    }
    this.current = new RelationshipGraphPanel(dependencies, folder);
  }

  public dispose(): void {
    if (this.disposed) return;
    this.disposed = true;
    this.active?.abort();
    if (RelationshipGraphPanel.current === this) RelationshipGraphPanel.current = undefined;
    for (const disposable of this.disposables.splice(0)) disposable.dispose();
  }

  private async receive(message: unknown): Promise<void> {
    const parsed = parseRelationshipGraphMessage(message);
    if (parsed === undefined || this.disposed) return;
    if (parsed.type === 'ready' || parsed.type === 'refresh') { await this.load(); return; }
    if (parsed.type === 'openAdr') { await this.openAdr(parsed.path); return; }
    if (parsed.type === 'filter') {
      const statuses = this.report === undefined ? [] : relationshipGraphStatuses(this.report);
      if (parsed.status !== null && !statuses.includes(parsed.status)) return;
      this.filters = { status: parsed.status, type: parsed.relationshipType, focusPath: null };
      await this.postGraph();
      return;
    }
    if (parsed.path !== null && !this.isKnownAdrPath(parsed.path)) return;
    this.filters = { ...this.filters, focusPath: parsed.path };
    await this.postGraph();
  }

  private async load(): Promise<void> {
    this.active?.abort();
    this.report = undefined;
    this.filters = { status: null, type: null, focusPath: null };
    const operation = new AbortController();
    this.active = operation;
    await this.post({ type: 'loading', strings: strings() });
    try {
      const configuration = readValidationConfiguration(this.folder.uri);
      const root = await resolveWorkspaceRoot(asLocation(this.folder));
      await ensureResourceWithinWorkspace(resolveWorkspaceRelativePath(root, configuration.directory), root);
      const capability = await this.dependencies.executor.execute(this.folder, ['relationships', '--help'], operation.signal);
      if (capability.result.termination !== 'exited' || capability.result.exitCode !== 0 || !capability.result.stdout.includes('adr-guard relationships')) {
        throw new IncompatibleRelationshipsCliError();
      }
      const execution = await this.dependencies.executor.execute(this.folder, ['relationships', configuration.directory, '--format', 'json', '--adr-format', configuration.adrFormat], operation.signal);
      if (execution.result.termination !== 'exited') requireSuccessfulResult(execution.result);
      if (execution.result.exitCode !== 0 && execution.result.exitCode !== 1) {
        if (execution.result.exitCode === 2) throw new IncompatibleRelationshipsCliError();
        requireSuccessfulResult(execution.result);
      }
      const report = parseRelationshipGraphReport(execution.result.stdout.trim());
      if (this.active !== operation || operation.signal.aborted) return;
      this.report = report;
      this.filters = { status: null, type: null, focusPath: null };
      await this.postGraph();
    } catch (error: unknown) {
      if (this.active !== operation || operation.signal.aborted) return;
      const state = error instanceof IncompatibleRelationshipsCliError || error instanceof RelationshipGraphContractError ? 'incompatible'
        : error instanceof RelationshipGraphReportLimitError ? 'limit' : 'error';
      this.dependencies.log.info(`Relationship graph failed: ${safeLogError(error)}`);
      await this.post({ type: 'graphError', state, message: relationshipErrorMessage(error) });
    } finally {
      if (this.active === operation) this.active = undefined;
    }
  }

  private async openAdr(relativePath: string): Promise<void> {
    try {
      if (!this.isKnownAdrPath(relativePath)) throw new WorkspacePolicyError('The requested ADR is not part of the loaded graph.', 'invalid-path');
      const root = await resolveWorkspaceRoot(asLocation(this.folder));
      const target = await ensureExistingMarkdownFileWithinWorkspace(relativePath, root);
      await vscode.window.showTextDocument(vscode.Uri.file(target), { preview: true });
    } catch (error: unknown) {
      this.dependencies.log.info(`Relationship graph navigation failed: ${safeLogError(error)}`);
      await vscode.window.showErrorMessage(vscode.l10n.t('The selected ADR could not be opened safely.'));
    }
  }

  private isKnownAdrPath(relativePath: string): boolean {
    return relativePath.length <= 4096 && (this.report?.nodes.some((node) => node.path === relativePath) ?? false);
  }

  private async postGraph(): Promise<void> {
    if (this.report === undefined) return;
    await this.post({
      type: 'graph', workspace: this.folder.name, directory: this.report.adrDirectory, summary: this.report.summary,
      diagnostics: this.report.diagnostics, statuses: relationshipGraphStatuses(this.report), filters: this.filters,
      visibleLimit: maximumVisibleRelationshipGraphNodes, layout: layoutRelationshipGraph(this.report, this.filters),
    });
  }

  private async post(message: unknown): Promise<void> { if (!this.disposed) await this.panel.webview.postMessage(message); }
}

function strings(): Record<string, string> { return {
  title: vscode.l10n.t('ADR Relationships'), refresh: vscode.l10n.t('Refresh'), loading: vscode.l10n.t('Loading ADR relationships…'),
  empty: vscode.l10n.t('No ADRs were found in the configured directory.'), noRelationships: vscode.l10n.t('No declared ADR relationships were found.'),
  nodes: vscode.l10n.t('ADRs'), relationships: vscode.l10n.t('Relationships'), resolved: vscode.l10n.t('Resolved'), unresolved: vscode.l10n.t('Unresolved'), ambiguous: vscode.l10n.t('Ambiguous'),
  dependsOn: vscode.l10n.t('Depends on'), supersedes: vscode.l10n.t('Supersedes'), supersededBy: vscode.l10n.t('Superseded by'), diagnostics: vscode.l10n.t('Relationship diagnostics'),
  openAdr: vscode.l10n.t('Open ADR'), retry: vscode.l10n.t('Retry'), unknownTitle: vscode.l10n.t('Unknown title'), unknownStatus: vscode.l10n.t('Unknown status'),
  filters: vscode.l10n.t('Graph filters'), allStatuses: vscode.l10n.t('All statuses'), allRelationshipTypes: vscode.l10n.t('All relationship types'),
  status: vscode.l10n.t('Status'), relationshipType: vscode.l10n.t('Relationship type'), clearFilters: vscode.l10n.t('Clear filters'),
  details: vscode.l10n.t('ADR details'), path: vscode.l10n.t('Path'), format: vscode.l10n.t('Format'), selectHint: vscode.l10n.t('Select an ADR to inspect its details.'),
  focus: vscode.l10n.t('Focus on direct relationships'), clearFocus: vscode.l10n.t('Show the complete graph'), focused: vscode.l10n.t('Showing the selected ADR and its direct relationships.'),
  direction: vscode.l10n.t('Arrows point from the declaring ADR to the declared target.'), cycle: vscode.l10n.t('Supersession cycle'), broken: vscode.l10n.t('Broken reference'),
  workspaceChanges: vscode.l10n.t('Workspace changes are not loaded automatically. Use Refresh to update this graph.'),
  visibleNodes: vscode.l10n.t('{0} of {1} matching ADRs shown'), visualLimit: vscode.l10n.t('Refine the filters or focus an ADR. At most {0} ADRs are shown at once.'),
  keyboardHelp: vscode.l10n.t('Use the arrow keys to move between ADRs, Enter or Space to select, and Home or End to jump.'),
  noMatches: vscode.l10n.t('No ADRs match the current graph filters.'),
}; }
function relationshipErrorMessage(error: unknown): string {
  if (error instanceof IncompatibleRelationshipsCliError || error instanceof RelationshipGraphContractError) return vscode.l10n.t('The installed ADR Guard CLI is incompatible with relationship graph schema 1.0. Update the CLI and try again.');
  if (error instanceof RelationshipGraphReportLimitError) return vscode.l10n.t('This graph has {0} ADRs and {1} relationships and exceeds the safe report limit.', error.nodes, error.relationships);
  if (error instanceof WorkspacePolicyError) return vscode.l10n.t('The configured ADR directory is unavailable or outside the selected trusted workspace.');
  if (error instanceof CliDiscoveryError) return vscode.l10n.t('ADR Guard was not found. Configure adrGuard.cli.path or install it on the extension host PATH.');
  if (error instanceof Error) return error.message;
  return vscode.l10n.t('ADR Guard failed for an unknown reason.');
}
function safeLogError(error: unknown): string { return (error instanceof Error ? error.message : 'unknown error').replace(/[\r\n\0]/gu, ' ').slice(0, 300); }

export async function openRelationshipGraph(dependencies: RelationshipGraphDependencies): Promise<void> {
  try { await RelationshipGraphPanel.show(dependencies); }
  catch (error: unknown) {
    dependencies.log.info(`Relationship graph command failed: ${safeLogError(error)}`);
    const detail = error instanceof Error ? error.message : vscode.l10n.t('ADR Guard failed for an unknown reason.');
    await vscode.window.showErrorMessage(detail);
  }
}
