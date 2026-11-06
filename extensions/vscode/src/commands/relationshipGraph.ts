import path from 'node:path';
import { randomBytes } from 'node:crypto';
import * as vscode from 'vscode';
import { CliDiscoveryError } from '../cli/discovery';
import { CliExecutor, requireSuccessfulResult } from '../cli/executor';
import { readValidationConfiguration } from '../configuration';
import { parseRelationshipGraphReport, RelationshipGraphContractError, RelationshipGraphRenderLimitError } from '../contracts/relationshipGraph';
import { OperationalLog } from '../logging';
import { asLocation, selectWorkspaceFolder } from '../workspace';
import { relationshipGraphWebviewHtml } from '../webview/relationshipGraphHtml';
import { layoutRelationshipGraph } from '../webview/relationshipGraphModel';
import { ensureResourceWithinWorkspace, resolveWorkspaceRelativePath, resolveWorkspaceRoot, WorkspacePolicyError } from '../workspacePolicy';

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
    if (!isMessage(message) || this.disposed) return;
    if (message.type === 'ready' || message.type === 'refresh') { await this.load(); return; }
    if (typeof message.path === 'string' && message.path.length <= 4096) await this.openAdr(message.path);
  }

  private async load(): Promise<void> {
    this.active?.abort();
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
      await this.post({ type: 'graph', workspace: this.folder.name, directory: report.adrDirectory, summary: report.summary, diagnostics: report.diagnostics, layout: layoutRelationshipGraph(report) });
    } catch (error: unknown) {
      if (this.active !== operation || operation.signal.aborted) return;
      const state = error instanceof IncompatibleRelationshipsCliError || error instanceof RelationshipGraphContractError ? 'incompatible'
        : error instanceof RelationshipGraphRenderLimitError ? 'limit' : 'error';
      this.dependencies.log.info(`Relationship graph failed: ${safeLogError(error)}`);
      await this.post({ type: 'graphError', state, message: relationshipErrorMessage(error) });
    } finally {
      if (this.active === operation) this.active = undefined;
    }
  }

  private async openAdr(relativePath: string): Promise<void> {
    try {
      const root = await resolveWorkspaceRoot(asLocation(this.folder));
      const target = await ensureResourceWithinWorkspace(path.resolve(root, relativePath), root);
      await vscode.window.showTextDocument(vscode.Uri.file(target), { preview: true });
    } catch (error: unknown) {
      this.dependencies.log.info(`Relationship graph navigation failed: ${safeLogError(error)}`);
      await vscode.window.showErrorMessage(vscode.l10n.t('The selected ADR could not be opened safely.'));
    }
  }

  private async post(message: unknown): Promise<void> { if (!this.disposed) await this.panel.webview.postMessage(message); }
}

function isMessage(value: unknown): value is { readonly type: 'ready' | 'refresh' | 'openAdr'; readonly path?: string } {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) return false;
  const record = value as Record<string, unknown>;
  return Object.keys(record).every((key) => key === 'type' || key === 'path') && (record.type === 'ready' || record.type === 'refresh' || record.type === 'openAdr');
}
function strings(): Record<string, string> { return {
  title: vscode.l10n.t('ADR Relationships'), refresh: vscode.l10n.t('Refresh'), loading: vscode.l10n.t('Loading ADR relationships…'),
  empty: vscode.l10n.t('No ADRs were found in the configured directory.'), noRelationships: vscode.l10n.t('No declared ADR relationships were found.'),
  nodes: vscode.l10n.t('ADRs'), relationships: vscode.l10n.t('Relationships'), resolved: vscode.l10n.t('Resolved'), unresolved: vscode.l10n.t('Unresolved'), ambiguous: vscode.l10n.t('Ambiguous'),
  dependsOn: vscode.l10n.t('Depends on'), supersedes: vscode.l10n.t('Supersedes'), supersededBy: vscode.l10n.t('Superseded by'), diagnostics: vscode.l10n.t('Relationship diagnostics'),
  openAdr: vscode.l10n.t('Open ADR'), retry: vscode.l10n.t('Retry'), unknownTitle: vscode.l10n.t('Unknown title'), unknownStatus: vscode.l10n.t('Unknown status'),
}; }
function relationshipErrorMessage(error: unknown): string {
  if (error instanceof IncompatibleRelationshipsCliError || error instanceof RelationshipGraphContractError) return vscode.l10n.t('The installed ADR Guard CLI is incompatible with relationship graph schema 1.0. Update the CLI and try again.');
  if (error instanceof RelationshipGraphRenderLimitError) return vscode.l10n.t('This graph has {0} ADRs and {1} relationships. The visual limit is 200 ADRs and 400 relationships.', error.nodes, error.relationships);
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
