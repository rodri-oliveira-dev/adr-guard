import path from 'node:path';
import { randomBytes } from 'node:crypto';
import * as vscode from 'vscode';
import { CliDiscoveryError } from '../cli/discovery';
import { CliExecutor, requireSuccessfulResult, safeErrorLine } from '../cli/executor';
import {
  newAdrFormSchemaVersion,
  parseNewAdrFormResponse,
  type NewAdrFormOperation,
  type NewAdrFormResponse,
} from '../contracts/newForm';
import { readValidationConfiguration } from '../configuration';
import { OperationalLog } from '../logging';
import { asLocation } from '../workspace';
import {
  ensureResourceWithinWorkspace,
  isResolvedPathWithin,
  resolveWorkspaceRelativePath,
  resolveWorkspaceRoot,
  WorkspacePolicyError,
} from '../workspacePolicy';
import {
  parseNewAdrFormMessage,
  type FormValues,
  VisualCreationSubmission,
} from '../webview/newAdrFormMessages';

interface NewAdrFormDependencies {
  readonly executor: CliExecutor;
  readonly log: OperationalLog;
  readonly extensionUri: vscode.Uri;
  readonly refreshExplorer: () => void;
}

export class NewAdrFormPanel implements vscode.Disposable {
  private readonly panel: vscode.WebviewPanel;
  private readonly disposables: vscode.Disposable[] = [];
  private activeOperation: AbortController | undefined;
  private readonly submission = new VisualCreationSubmission();
  private disposed = false;

  private constructor(private readonly dependencies: NewAdrFormDependencies) {
    this.panel = vscode.window.createWebviewPanel(
      'adrGuard.newAdrForm',
      vscode.l10n.t('Create ADR with Form'),
      vscode.ViewColumn.Active,
      {
        enableScripts: true,
        retainContextWhenHidden: true,
        localResourceRoots: [vscode.Uri.joinPath(dependencies.extensionUri, 'media')],
      },
    );
    this.panel.webview.html = this.html();
    this.disposables.push(
      this.panel,
      this.panel.onDidDispose(() => this.dispose()),
      this.panel.webview.onDidReceiveMessage(async (message: unknown) => this.receive(message)),
    );
  }

  public static async show(dependencies: NewAdrFormDependencies): Promise<void> {
    if (!vscode.workspace.isTrusted) {
      await vscode.window.showErrorMessage(vscode.l10n.t('Trust this workspace before running the ADR Guard CLI.'));
      return;
    }
    const folders = (vscode.workspace.workspaceFolders ?? []).filter((folder) => folder.uri.scheme === 'file');
    if (folders.length === 0) {
      await vscode.window.showErrorMessage(vscode.l10n.t('Open a local folder or workspace before running ADR Guard.'));
      return;
    }
    new NewAdrFormPanel(dependencies);
  }

  public dispose(): void {
    if (this.disposed) return;
    this.disposed = true;
    this.activeOperation?.abort();
    this.activeOperation = undefined;
    for (const disposable of this.disposables.splice(0)) disposable.dispose();
  }

  private async receive(value: unknown): Promise<void> {
    const message = parseNewAdrFormMessage(value);
    if (message === undefined || this.disposed) {
      if (!this.disposed) await this.post({ type: 'error', message: vscode.l10n.t('The form sent an invalid message.') });
      return;
    }
    if (message.type === 'cancel') {
      this.panel.dispose();
      return;
    }
    if (!this.submission.accepts(message.type)) return;
    if (message.type === 'traditional') {
      this.panel.dispose();
      await vscode.commands.executeCommand('adrGuard.createAdr');
      return;
    }
    if (message.type === 'ready') {
      await this.initialize();
      return;
    }
    if (message.values === undefined) return;
    if (message.type === 'create' && !this.submission.begin()) return;
    await this.run(message.type, message.values);
  }

  private async initialize(): Promise<void> {
    const folders = (vscode.workspace.workspaceFolders ?? []).filter((folder) => folder.uri.scheme === 'file');
    const activeFolder = vscode.window.activeTextEditor === undefined
      ? undefined
      : vscode.workspace.getWorkspaceFolder(vscode.window.activeTextEditor.document.uri);
    const initial = activeFolder?.uri.scheme === 'file' ? activeFolder : folders[0];
    await this.post({
      type: 'initialize',
      workspaces: folders.map((folder) => ({
        id: folder.uri.toString(),
        name: folder.name,
        directory: readValidationConfiguration(folder.uri).directory,
      })),
      initialWorkspace: initial?.uri.toString(),
      initialCulture: vscode.env.language.toLowerCase() === 'pt-br' ? 'pt-BR' : 'en-US',
      strings: webviewStrings(),
    });
  }

  private async run(operation: NewAdrFormOperation, values: FormValues): Promise<void> {
    this.activeOperation?.abort();
    const controller = new AbortController();
    this.activeOperation = controller;
    await this.post({ type: 'busy', operation });
    try {
      const folder = workspaceFolder(values.workspace);
      const root = await resolveWorkspaceRoot(asLocation(folder));
      const target = await ensureResourceWithinWorkspace(
        resolveWorkspaceRelativePath(root, values.directory),
        root,
      );
      const args = buildFormArguments(target, values.template, values.culture);
      const input = JSON.stringify({
        schemaVersion: newAdrFormSchemaVersion,
        operation,
        ...(operation === 'describe' ? {} : { title: values.title, fields: values.fields }),
      });
      const execution = await this.dependencies.executor.execute(folder, args, controller.signal, input);
      if (execution.result.termination === 'exited' && execution.result.exitCode === 2) {
        throw new FormCapabilityError(safeErrorLine(execution.result.stderr));
      }
      requireSuccessfulResult(execution.result);
      const response = parseNewAdrFormResponse(execution.result.stdout.trim());
      validateResponseForOperation(response, operation);
      if (operation === 'create') {
        const reported = response.path;
        if (reported === undefined || !response.written) {
          throw new Error(vscode.l10n.t('The CLI did not confirm the created ADR.'));
        }
        const safePath = await ensureResourceWithinWorkspace(path.resolve(root, reported), root);
        if (!isResolvedPathWithin(safePath, target)) {
          throw new Error(vscode.l10n.t('The CLI reported an unsafe or unexpected created file path.'));
        }
        await vscode.window.showTextDocument(vscode.Uri.file(safePath));
        this.dependencies.refreshExplorer();
        await vscode.window.showInformationMessage(vscode.l10n.t('ADR Guard created {0}.', path.basename(safePath)));
        await this.post({ type: 'created' });
        this.panel.dispose();
        return;
      }
      await this.post({ type: 'result', response });
    } catch (error: unknown) {
      if (controller.signal.aborted) return;
      this.dependencies.log.info(`Visual ADR form operation failed: ${safeLogError(error)}`);
      await this.post({
        type: 'error',
        message: formErrorMessage(error),
        traditionalAvailable: error instanceof FormCapabilityError || error instanceof CliDiscoveryError,
      });
    } finally {
      if (this.activeOperation === controller) this.activeOperation = undefined;
      if (operation === 'create') this.submission.end();
      if (!this.disposed) await this.post({ type: 'idle', operation });
    }
  }

  private async post(message: unknown): Promise<void> {
    if (!this.disposed) await this.panel.webview.postMessage(message);
  }

  private html(): string {
    const nonce = createNonce();
    const script = this.panel.webview.asWebviewUri(vscode.Uri.joinPath(this.dependencies.extensionUri, 'media', 'newAdrForm.js'));
    const style = this.panel.webview.asWebviewUri(vscode.Uri.joinPath(this.dependencies.extensionUri, 'media', 'newAdrForm.css'));
    return `<!DOCTYPE html>
<html lang="${escapeAttribute(vscode.env.language)}">
<head>
  <meta charset="UTF-8">
  <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src ${this.panel.webview.cspSource}; script-src 'nonce-${nonce}';">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <link rel="stylesheet" href="${style.toString()}">
  <title>${escapeHtml(vscode.l10n.t('Create ADR with Form'))}</title>
</head>
<body>
  <main id="app" aria-live="polite"></main>
  <script nonce="${nonce}" src="${script.toString()}"></script>
</body>
</html>`;
  }
}

class FormCapabilityError extends Error {
  public constructor(detail?: string) {
    super(detail);
    this.name = 'FormCapabilityError';
  }
}

function buildFormArguments(
  directory: string,
  template: FormValues['template'],
  culture: FormValues['culture'],
): string[] {
  return [
    'new', directory, '--form',
    ...(template === 'configured' ? [] : ['--template', template]),
    '--culture', culture,
  ];
}

function workspaceFolder(id: string): vscode.WorkspaceFolder {
  const folder = vscode.workspace.workspaceFolders?.find((candidate) => candidate.uri.toString() === id);
  if (folder?.uri.scheme !== 'file') {
    throw new WorkspacePolicyError('The selected workspace folder is unavailable.', 'missing');
  }
  return folder;
}

function validateResponseForOperation(response: NewAdrFormResponse, operation: NewAdrFormOperation): void {
  if (response.operation !== operation) throw new Error(vscode.l10n.t('The CLI returned a response for a different form operation.'));
  if (operation === 'preview' && (response.markdown === undefined || response.written)) {
    throw new Error(vscode.l10n.t('The CLI returned an invalid preview response.'));
  }
  if (operation === 'describe' && (response.path !== undefined || response.markdown !== undefined || response.written)) {
    throw new Error(vscode.l10n.t('The CLI returned an invalid template description.'));
  }
}

function formErrorMessage(error: unknown): string {
  if (error instanceof FormCapabilityError) {
    return vscode.l10n.t('The installed ADR Guard CLI does not support visual creation. Install CLI 1.7.0 or newer, or use the traditional creation flow.');
  }
  if (error instanceof WorkspacePolicyError) {
    return error.code === 'outside-workspace' || error.code === 'invalid-path'
      ? vscode.l10n.t('The ADR destination must be a safe directory inside the selected workspace.')
      : vscode.l10n.t('Open a trusted local workspace before using the ADR form.');
  }
  if (error instanceof CliDiscoveryError) {
    return vscode.l10n.t('ADR Guard was not found. Configure adrGuard.cli.path or install it on the extension host PATH.');
  }
  return error instanceof Error ? error.message : vscode.l10n.t('ADR Guard failed for an unknown reason.');
}

function webviewStrings(): Record<string, string> {
  const values = [
    'Create ADR with Form', 'Workspace folder', 'ADR destination directory', 'Creation template',
    'Document language', 'Decision title', 'Markdown preview', 'Create ADR', 'Cancel',
    'Use traditional creation', 'Configured template', 'Minimal', 'Extended',
    'Minimal captures the context, decision, and consequences.',
    'Extended adds optional drivers, alternatives, rationale, risks, and references.',
    'This custom template cannot be mapped safely to form fields. Use the traditional creation flow to preserve it.',
    'Required', 'Optional', 'Preview updates after you edit the form.', 'No file is written until you choose Create ADR.',
    'Context', 'Decision drivers', 'Options considered', 'Decision', 'Rationale', 'Consequences',
    'Positive consequences', 'Negative consequences', 'Risks', 'References',
    'Describe the problem, constraints, and relevant stakeholders.',
    'List requirements, quality attributes, constraints, and trade-offs.',
    'Compare viable alternatives, including keeping the current approach.',
    'State the proposed choice, its scope, and why it addresses the problem.',
    'Explain why this option best satisfies the decision drivers.',
    'Describe expected impact and trade-offs.', 'Record expected benefits and assumptions.',
    'Record costs, limitations, and added complexity.', 'Identify uncertainties, failure modes, mitigations, and owners.',
    'Add links to evidence, related documents, or ADRs.', 'Loading template…', 'Creating ADR…',
    'Preview unavailable until required fields are complete.',
  ];
  return Object.fromEntries(values.map((value) => [value, vscode.l10n.t(value)]));
}

function safeLogError(error: unknown): string {
  return (error instanceof Error ? error.message : 'unknown error').replace(/[\r\n\0]/gu, ' ').slice(0, 300);
}

function createNonce(): string {
  return randomBytes(16).toString('base64');
}

function escapeHtml(value: string): string {
  return value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
}

function escapeAttribute(value: string): string {
  return escapeHtml(value).replaceAll('"', '&quot;').replaceAll("'", '&#39;');
}
