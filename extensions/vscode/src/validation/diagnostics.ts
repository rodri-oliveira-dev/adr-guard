import { lstat, realpath } from 'node:fs/promises';
import path from 'node:path';
import * as vscode from 'vscode';
import { CliExecutor, safeErrorLine } from '../cli/executor';
import { CheckReportError, parseCheckReport, type AdrCheckReport } from '../contracts/checkReport';
import { OperationalLog } from '../logging';
import { buildCheckArguments, type AdrFormat } from '../commands/arguments';
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

export interface ValidationOptions {
  readonly adrFormat?: AdrFormat;
  readonly changed?: boolean;
  readonly baseReference?: string;
  readonly baselineSetting?: string;
}

export class CliCapabilityError extends Error {
  public constructor(public readonly options: readonly string[]) {
    super(vscode.l10n.t(
      'The installed ADR Guard CLI does not support the requested option(s): {0}. Install a CLI build with the v1.4 contracts.',
      options.join(', '),
    ));
    this.name = 'CliCapabilityError';
  }
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
    options: ValidationOptions = {},
  ): Promise<ValidationResult> {
    const workspaceRoot = await resolveWorkspaceRoot(asLocation(folder));
    const configuredDirectory = resolveWorkspaceRelativePath(workspaceRoot, directorySetting);
    const adrDirectory = await ensureResourceWithinWorkspace(configuredDirectory, workspaceRoot);
    const baselinePath = options.baselineSetting === undefined
      ? undefined
      : await resolveBaselinePath(options.baselineSetting, workspaceRoot);
    const args = buildCheckArguments({
      directory: adrDirectory,
      adrFormat: options.adrFormat ?? 'canonical',
      ...(options.changed === undefined ? {} : { changed: options.changed }),
      ...(options.baseReference === undefined ? {} : { baseReference: options.baseReference }),
      ...(baselinePath === undefined ? {} : { baselinePath }),
    });
    const execution = await this.executor.execute(folder, args, signal);
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
      const requestedOptions = advancedOptions(args);
      if (result.exitCode === 2 && requestedOptions.length > 0) {
        throw new CliCapabilityError(requestedOptions);
      }
      const detail = safeErrorLine(result.stderr);
      throw new Error(detail === undefined
        ? vscode.l10n.t(
          'ADR Guard failed with exit code {0}. See ADR Guard Output for details.',
          result.exitCode ?? 'unknown',
        )
        : vscode.l10n.t('ADR Guard failed with exit code {0}. {1}', result.exitCode ?? 'unknown', detail));
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
        finding.baselineState === undefined
          ? finding.message
          : finding.baselineState === 'new'
            ? vscode.l10n.t('New since baseline: {0}', finding.message)
            : vscode.l10n.t('Existing in baseline: {0}', finding.message),
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

async function resolveBaselinePath(setting: string, workspaceRoot: string): Promise<string> {
  const candidate = resolveWorkspaceRelativePath(workspaceRoot, setting);
  let candidateStat;
  try {
    candidateStat = await lstat(candidate);
  } catch (error: unknown) {
    throw new Error(vscode.l10n.t('The configured diagnostic baseline is missing or inaccessible.'), { cause: error });
  }
  if (!candidateStat.isFile() || candidateStat.isSymbolicLink()) {
    throw new Error(vscode.l10n.t('The diagnostic baseline must be a regular, non-symbolic-link JSON file.'));
  }
  const safe = await ensureResourceWithinWorkspace(candidate, workspaceRoot);
  if (path.extname(safe).toLowerCase() !== '.json') {
    throw new Error(vscode.l10n.t('The diagnostic baseline must be a JSON file inside the selected workspace.'));
  }
  return candidate;
}

function advancedOptions(args: readonly string[]): string[] {
  return ['--adr-format', '--changed', '--baseline'].filter((option) => args.includes(option));
}
