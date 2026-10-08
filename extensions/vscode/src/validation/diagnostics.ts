import { realpath } from 'node:fs/promises';
import path from 'node:path';
import * as vscode from 'vscode';
import { CliExecutor } from '../cli/executor';
import { CheckReportError, parseCheckReport, type AdrCheckReport } from '../contracts/checkReport';
import { OperationalLog } from '../logging';
import { asLocation } from '../workspace';
import {
  ensureResourceWithinWorkspace,
  isResolvedPathWithin,
  resolveWorkspaceRelativePath,
  resolveWorkspaceRoot,
} from '../workspacePolicy';

export interface ValidationResult {
  readonly report: AdrCheckReport;
  readonly workspaceRoot: string;
  readonly adrDirectory: string;
}

export class DiagnosticManager implements vscode.Disposable {
  private readonly collection = vscode.languages.createDiagnosticCollection('ADR Guard');
  private readonly ownedUris = new Map<string, Set<string>>();

  public constructor(
    private readonly executor: CliExecutor,
    private readonly log: OperationalLog,
  ) {}

  public async validate(
    folder: vscode.WorkspaceFolder,
    directorySetting: string,
    signal?: AbortSignal,
  ): Promise<ValidationResult> {
    const workspaceRoot = await resolveWorkspaceRoot(asLocation(folder));
    const configuredDirectory = resolveWorkspaceRelativePath(workspaceRoot, directorySetting);
    const adrDirectory = await ensureResourceWithinWorkspace(configuredDirectory, workspaceRoot);
    const execution = await this.executor.execute(
      folder,
      ['check', adrDirectory, '--format', 'json'],
      signal,
    );
    const { result } = execution;
    if (result.termination !== 'exited') {
      if (result.termination === 'cancelled') {
        throw new DOMException('Validation cancelled', 'AbortError');
      }
      const message = result.termination === 'timed-out'
        ? vscode.l10n.t('ADR Guard operation timed out. Adjust adrGuard.cli.timeoutMilliseconds if needed.')
        : result.termination === 'output-limit'
          ? vscode.l10n.t('ADR Guard exceeded the configured output limit and was stopped.')
          : vscode.l10n.t('ADR Guard could not be started.');
      throw new Error(message);
    }
    if (result.exitCode !== 0 && result.exitCode !== 1) {
      throw new Error(vscode.l10n.t(
        'ADR Guard failed with exit code {0}. See ADR Guard Output for details.',
        result.exitCode ?? 'unknown',
      ));
    }
    let report: AdrCheckReport;
    try {
      report = parseCheckReport(result.stdout);
    } catch (error: unknown) {
      const detail = error instanceof CheckReportError ? error.message : 'unknown contract error';
      throw new Error(vscode.l10n.t('ADR Guard returned invalid or incompatible JSON: {0}', detail), { cause: error });
    }
    if ((result.exitCode === 0 && !report.valid) || (result.exitCode === 1 && report.valid)) {
      throw new Error(vscode.l10n.t('ADR Guard returned invalid or incompatible JSON: {0}', 'exit code and valid disagree'));
    }
    signal?.throwIfAborted();
    const diagnostics = await this.toDiagnostics(report, execution.workspaceRoot, adrDirectory);
    signal?.throwIfAborted();
    this.replaceWorkspaceDiagnostics(workspaceRoot, diagnostics);
    return { report, workspaceRoot, adrDirectory };
  }

  public clearWorkspace(workspaceRoot: string): void {
    const owned = this.ownedUris.get(workspaceRoot);
    if (owned !== undefined) {
      for (const value of owned) {
        this.collection.delete(vscode.Uri.parse(value));
      }
    }
    this.ownedUris.delete(workspaceRoot);
  }

  public delete(uri: vscode.Uri): void {
    this.collection.delete(uri);
    for (const owned of this.ownedUris.values()) {
      owned.delete(uri.toString());
    }
  }

  public dispose(): void {
    this.ownedUris.clear();
    this.collection.dispose();
  }

  private async toDiagnostics(
    report: AdrCheckReport,
    workspaceRoot: string,
    adrDirectory: string,
  ): Promise<Map<string, { uri: vscode.Uri; diagnostics: vscode.Diagnostic[] }>> {
    const grouped = new Map<string, { uri: vscode.Uri; diagnostics: vscode.Diagnostic[] }>();
    const realAdrDirectory = await realpath(adrDirectory);
    for (const finding of report.diagnostics) {
      const candidate = path.resolve(workspaceRoot, finding.file);
      const safePath = await ensureResourceWithinWorkspace(candidate, workspaceRoot);
      if (!isResolvedPathWithin(safePath, realAdrDirectory)) {
        throw new CheckReportError('diagnostic file resolves outside the ADR directory');
      }
      const uri = vscode.Uri.file(safePath);
      const diagnostic = new vscode.Diagnostic(
        new vscode.Range(0, 0, 0, 0),
        finding.message,
        vscode.DiagnosticSeverity.Error,
      );
      diagnostic.code = finding.code;
      diagnostic.source = 'ADR Guard';
      const key = uri.toString();
      const existing = grouped.get(key);
      if (existing === undefined) {
        grouped.set(key, { uri, diagnostics: [diagnostic] });
      } else {
        existing.diagnostics.push(diagnostic);
      }
    }
    return grouped;
  }

  private replaceWorkspaceDiagnostics(
    workspaceRoot: string,
    diagnostics: Map<string, { uri: vscode.Uri; diagnostics: vscode.Diagnostic[] }>,
  ): void {
    this.clearWorkspace(workspaceRoot);
    const owned = new Set<string>();
    for (const [key, value] of diagnostics) {
      this.collection.set(value.uri, value.diagnostics);
      owned.add(key);
    }
    this.ownedUris.set(workspaceRoot, owned);
    this.log.info(`Published ${[...diagnostics.values()].reduce((total, item) => total + item.diagnostics.length, 0)} ADR Guard diagnostic(s).`);
  }
}
