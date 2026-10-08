import path from 'node:path';
import * as vscode from 'vscode';
import { CliExecutor, requireSuccessfulResult, withCancellationProgress } from '../cli/executor';
import { CliDiscoveryError } from '../cli/discovery';
import { readValidationConfiguration } from '../configuration';
import { OperationalLog } from '../logging';
import { CliCapabilityError, DiagnosticManager } from '../validation/diagnostics';
import { selectWorkspaceFolder, asLocation } from '../workspace';
import {
  ensureResourceWithinWorkspace,
  isResolvedPathWithin,
  resolveWorkspaceRelativePath,
  resolveWorkspaceRoot,
  WorkspacePolicyError,
} from '../workspacePolicy';
import {
  buildInitArguments,
  buildIndexArguments,
  buildNewArguments,
  isSafeGitBaseReference,
  type AdrFormat,
  parseCreatedAdrPath,
  type TemplateSelection,
} from './arguments';

export interface CoreCommandDependencies {
  readonly executor: CliExecutor;
  readonly diagnostics: DiagnosticManager;
  readonly log: OperationalLog;
}

export async function initializeRepository(dependencies: CoreCommandDependencies): Promise<void> {
  await runExplicit(dependencies.log, async () => {
    const selected = await selectWorkspaceFolder();
    if (selected === undefined) return;
    const root = await resolveWorkspaceRoot(asLocation(selected));
    const directory = await promptDirectory(selected);
    if (directory === undefined) return;
    const template = await promptTemplate(root, true);
    if (template === undefined) return;
    const workflow = await vscode.window.showQuickPick([
      { label: vscode.l10n.t('Include GitHub Actions workflow'), value: true },
      { label: vscode.l10n.t('Skip GitHub Actions workflow'), value: false },
    ], { placeHolder: vscode.l10n.t('Include GitHub Actions workflow') });
    if (workflow === undefined) return;
    const preview = await withCancellationProgress(
      vscode.l10n.t('ADR Guard initialization preview'),
      async (signal) => dependencies.executor.execute(selected, buildInitArguments({
        repository: root,
        adrDirectory: directory,
        template,
        githubActions: workflow.value,
        dryRun: true,
      }), signal),
    );
    requireSuccessfulResult(preview.result);
    dependencies.log.detail(
      vscode.l10n.t('Review the proposed changes in ADR Guard Output. No files have been changed.'),
      preview.result.stdout,
    );
    const confirm = await vscode.window.showInformationMessage(
      vscode.l10n.t('Initialize the repository with these changes? Existing files will not be overwritten.'),
      { modal: true },
      vscode.l10n.t('Initialize ADR Guard'),
    );
    if (confirm === undefined) return;
    const execution = await withCancellationProgress(
      vscode.l10n.t('Initialize ADR Guard'),
      async (signal) => dependencies.executor.execute(selected, buildInitArguments({
        repository: root,
        adrDirectory: directory,
        template,
        githubActions: workflow.value,
        dryRun: false,
      }), signal),
    );
    requireSuccessfulResult(execution.result);
    await vscode.window.showInformationMessage(vscode.l10n.t('ADR Guard initialized the repository.'));
  });
}

export async function createAdr(dependencies: CoreCommandDependencies): Promise<void> {
  await runExplicit(dependencies.log, async () => {
    const selected = await selectWorkspaceFolder();
    if (selected === undefined) return;
    const root = await resolveWorkspaceRoot(asLocation(selected));
    const directory = await promptDirectory(selected);
    if (directory === undefined) return;
    const target = await ensureResourceWithinWorkspace(resolveWorkspaceRelativePath(root, directory), root);
    const title = await vscode.window.showInputBox({
      title: vscode.l10n.t('ADR title'),
      prompt: vscode.l10n.t('ADR title'),
      ignoreFocusOut: true,
      validateInput: (value) => value.trim().length === 0 || /[\r\n]/u.test(value)
        ? vscode.l10n.t('ADR title')
        : undefined,
    });
    if (title === undefined) return;
    const template = await promptTemplate(root, false);
    if (template === undefined) return;
    const culture = await vscode.window.showQuickPick(
      [{ label: 'English (United States)', value: 'en-US' as const }, { label: 'Português (Brasil)', value: 'pt-BR' as const }],
      { placeHolder: vscode.l10n.t('Culture') },
    );
    if (culture === undefined) return;
    const execution = await withCancellationProgress(
      vscode.l10n.t('Create ADR'),
      async (signal) => dependencies.executor.execute(selected, buildNewArguments({
        adrDirectory: target,
        title: title.trim(),
        template,
        culture: culture.value,
      }), signal),
    );
    requireSuccessfulResult(execution.result);
    const reported = parseCreatedAdrPath(execution.result.stdout);
    const safePath = await ensureResourceWithinWorkspace(path.resolve(root, reported), root);
    if (!isResolvedPathWithin(safePath, target)) {
      throw new Error(vscode.l10n.t('The CLI reported an unsafe or unexpected created file path.'));
    }
    await vscode.window.showTextDocument(vscode.Uri.file(safePath));
    await vscode.window.showInformationMessage(vscode.l10n.t('ADR Guard created {0}.', path.basename(safePath)));
  });
}

export async function validateAdrs(dependencies: CoreCommandDependencies): Promise<void> {
  await runExplicit(dependencies.log, async () => {
    const selected = await selectWorkspaceFolder();
    if (selected === undefined) return;
    const configuration = readValidationConfiguration(selected.uri);
    const result = await withCancellationProgress(
      vscode.l10n.t('Validate ADRs'),
      async (signal) => dependencies.diagnostics.validate(
        selected,
        configuration.directory,
        signal,
        { adrFormat: configuration.adrFormat },
      ),
    );
    await showValidationResult(result.report);
  });
}

export async function validateChangedAdrs(dependencies: CoreCommandDependencies): Promise<void> {
  await runExplicit(dependencies.log, async () => {
    const selected = await selectWorkspaceFolder();
    if (selected === undefined) return;
    const baseReference = await vscode.window.showInputBox({
      title: vscode.l10n.t('Git base reference'),
      prompt: vscode.l10n.t('Enter a local Git reference such as main or origin/main. No fetch will be performed.'),
      value: 'main',
      ignoreFocusOut: true,
      validateInput: (value) => isSafeGitBaseReference(value.trim())
        ? undefined
        : vscode.l10n.t('Enter a non-empty Git reference that does not start with a dash or contain whitespace or control characters.'),
    });
    if (baseReference === undefined) return;
    const configuration = readValidationConfiguration(selected.uri);
    try {
      const result = await withCancellationProgress(
        vscode.l10n.t('Validate Changed ADRs'),
        async (signal) => dependencies.diagnostics.validate(
          selected,
          configuration.directory,
          signal,
          { adrFormat: configuration.adrFormat, changed: true, baseReference: baseReference.trim() },
        ),
      );
      await showValidationResult(result.report);
    } catch (error: unknown) {
      if (error instanceof DOMException && error.name === 'AbortError') throw error;
      const fallback = vscode.l10n.t('Run Full Validation');
      const selectedAction = await vscode.window.showErrorMessage(
        vscode.l10n.t('Incremental validation could not run: {0}', userErrorMessage(error)),
        fallback,
      );
      if (selectedAction !== fallback) return;
      const result = await withCancellationProgress(
        vscode.l10n.t('Validate ADRs'),
        async (signal) => dependencies.diagnostics.validate(
          selected,
          configuration.directory,
          signal,
          { adrFormat: configuration.adrFormat },
        ),
      );
      await showValidationResult(result.report);
    }
  });
}

export async function validateWithBaseline(dependencies: CoreCommandDependencies): Promise<void> {
  await runExplicit(dependencies.log, async () => {
    const selected = await selectWorkspaceFolder();
    if (selected === undefined) return;
    const configuration = readValidationConfiguration(selected.uri);
    const result = await withCancellationProgress(
      vscode.l10n.t('Validate ADRs with Baseline'),
      async (signal) => dependencies.diagnostics.validate(
        selected,
        configuration.directory,
        signal,
        { adrFormat: configuration.adrFormat, baselineSetting: configuration.baseline },
      ),
    );
    await showValidationResult(result.report);
  });
}

export async function selectAdrFormat(): Promise<void> {
  const selected = await selectWorkspaceFolder();
  if (selected === undefined) return;
  const current = readValidationConfiguration(selected.uri).adrFormat;
  const items: (vscode.QuickPickItem & { readonly value: AdrFormat })[] = [
    { label: 'Canonical', ...(current === 'canonical' ? { description: vscode.l10n.t('Current') } : {}), value: 'canonical' },
    { label: 'MADR 4.0', ...(current === 'madr-4' ? { description: vscode.l10n.t('Current') } : {}), value: 'madr-4' },
  ];
  const choice = await vscode.window.showQuickPick(items, {
    placeHolder: vscode.l10n.t('Select the ADR format for this workspace folder'),
  });
  if (choice === undefined) return;
  await vscode.workspace.getConfiguration('adrGuard.validation', selected.uri)
    .update('adrFormat', choice.value, vscode.ConfigurationTarget.WorkspaceFolder);
  await vscode.window.showInformationMessage(vscode.l10n.t('ADR format set to {0}.', formatLabel(choice.value)));
}

export async function generateIndex(dependencies: CoreCommandDependencies): Promise<void> {
  await runExplicit(dependencies.log, async () => {
    const selected = await selectWorkspaceFolder();
    if (selected === undefined) return;
    const confirm = await vscode.window.showWarningMessage(
      vscode.l10n.t('Generating the ADR index can modify README.md in the ADR directory. Continue?'),
      { modal: true },
      vscode.l10n.t('Generate Index'),
    );
    if (confirm === undefined) return;
    const root = await resolveWorkspaceRoot(asLocation(selected));
    const configuration = readValidationConfiguration(selected.uri);
    const adrDirectory = await ensureResourceWithinWorkspace(
      resolveWorkspaceRelativePath(root, configuration.directory),
      root,
    );
    const execution = await withCancellationProgress(
      vscode.l10n.t('Generate Index'),
      async (signal) => dependencies.executor.execute(
        selected,
        buildIndexArguments(adrDirectory, configuration.adrFormat),
        signal,
      ),
    );
    if (execution.result.termination === 'exited' && execution.result.exitCode === 2
      && configuration.adrFormat === 'madr-4') {
      throw new CliCapabilityError(['--adr-format']);
    }
    requireSuccessfulResult(execution.result);
    const index = await ensureResourceWithinWorkspace(path.join(adrDirectory, 'README.md'), root);
    if (!isResolvedPathWithin(index, adrDirectory)) {
      throw new Error(vscode.l10n.t('The index file was not created inside the configured ADR directory.'));
    }
    const document = await vscode.workspace.openTextDocument(vscode.Uri.file(index));
    await vscode.window.showTextDocument(document, { preview: true });
    await vscode.window.showInformationMessage(vscode.l10n.t('ADR Guard generated the ADR index.'));
  });
}

async function showValidationResult(report: import('../contracts/checkReport').AdrCheckReport): Promise<void> {
  if (report.baseline !== undefined) {
    const message = report.valid
      ? vscode.l10n.t(
        'Baseline validation passed: {0} new, {1} existing, {2} resolved.',
        report.baseline.new,
        report.baseline.existing,
        report.baseline.resolved,
      )
      : vscode.l10n.t(
        'Baseline validation failed: {0} new, {1} existing, {2} resolved.',
        report.baseline.new,
        report.baseline.existing,
        report.baseline.resolved,
      );
    await vscode.window.showInformationMessage(message);
    return;
  }
  await vscode.window.showInformationMessage(
    report.valid
      ? vscode.l10n.t('ADR validation completed with no new issues.')
      : vscode.l10n.t('ADR validation found {0} issue(s).', report.summary.diagnostics),
  );
}

function formatLabel(format: AdrFormat): string {
  return format === 'canonical' ? 'Canonical' : 'MADR 4.0';
}

async function promptDirectory(folder: vscode.WorkspaceFolder): Promise<string | undefined> {
  const configured = readValidationConfiguration(folder.uri).directory;
  const value = await vscode.window.showInputBox({
    title: vscode.l10n.t('ADR directory'),
    prompt: vscode.l10n.t('Enter a workspace-relative directory such as docs/adr'),
    value: configured,
    ignoreFocusOut: true,
    validateInput: (value) => value.trim().length === 0 || path.isAbsolute(value) || value.includes('\0')
      ? vscode.l10n.t('The ADR directory must be a relative path inside the workspace.')
      : undefined,
  });
  return value?.trim();
}

async function promptTemplate(root: string, init: boolean): Promise<TemplateSelection | undefined> {
  const selected = await vscode.window.showQuickPick([
    { label: vscode.l10n.t('Use configured/default template'), value: 'configured' as const },
    { label: vscode.l10n.t('Minimal'), value: 'minimal' as const },
    { label: vscode.l10n.t('Extended'), value: 'extended' as const },
    { label: vscode.l10n.t('Custom Markdown file…'), value: 'custom' as const },
  ], { placeHolder: vscode.l10n.t('Template') });
  if (selected === undefined) return undefined;
  if (selected.value !== 'custom') return selected.value;
  const picked = await vscode.window.showOpenDialog({
    canSelectMany: false,
    canSelectFiles: true,
    canSelectFolders: false,
    filters: { Markdown: ['md'] },
    defaultUri: vscode.Uri.file(root),
  });
  if (picked?.[0] === undefined) return undefined;
  const safe = await ensureResourceWithinWorkspace(picked[0].fsPath, root);
  return { file: init ? path.relative(root, safe) : safe };
}

async function runExplicit(log: OperationalLog, operation: () => Promise<void>): Promise<void> {
  try {
    await operation();
  } catch (error: unknown) {
    const message = userErrorMessage(error);
    log.info(`Explicit command failed: ${message}`);
    log.show();
    await vscode.window.showErrorMessage(message);
  }
}

function userErrorMessage(error: unknown): string {
  if (error instanceof WorkspacePolicyError) {
    switch (error.code) {
      case 'untrusted': return vscode.l10n.t('Trust this workspace before running the ADR Guard CLI.');
      case 'missing': return vscode.l10n.t('Open a local folder or workspace before running ADR Guard.');
      case 'non-file': return vscode.l10n.t('ADR Guard requires a local file-system workspace; virtual workspace resources are unsupported.');
      case 'outside-workspace': return vscode.l10n.t('The requested resource is outside the selected workspace.');
      case 'invalid-path': return vscode.l10n.t('The requested local path is invalid or inaccessible.');
    }
  }
  if (error instanceof CliDiscoveryError) {
    return error.code === 'not-found'
      ? vscode.l10n.t('ADR Guard was not found. Configure adrGuard.cli.path or install it on the extension host PATH.')
      : vscode.l10n.t('The configured ADR Guard executable is invalid or inaccessible.');
  }
  return error instanceof Error
    ? error.message
    : vscode.l10n.t('ADR Guard failed for an unknown reason.');
}
