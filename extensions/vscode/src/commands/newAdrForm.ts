import path from 'node:path';
import { randomBytes } from 'node:crypto';
import { readFileSync } from 'node:fs';
import * as vscode from 'vscode';
import { CliDiscoveryError } from '../cli/discovery';
import { CliExecutor, requireSuccessfulResult, safeErrorLine } from '../cli/executor';
import { newAdrFormSchemaVersion, parseNewAdrFormResponse, type NewAdrFormOperation, type NewAdrFormResponse } from '../contracts/newForm';
import { editFormSchemaVersion, parseEditFormResponse } from '../contracts/editForm';
import { readConfiguration, readCreationConfiguration, readFeatureConfiguration, readMcpConfiguration, readUiConfiguration, readValidationConfiguration } from '../configuration';
import type { InstallationCheckResult } from './checkInstallation';
import type { AdrExplorerProvider } from '../explorer/provider';
import { OperationalLog } from '../logging';
import { asLocation } from '../workspace';
import { ensurePotentialDirectoryWithinWorkspace, ensurePotentialFileWithinWorkspace, ensureResourceWithinWorkspace, isResolvedPathWithin, resolveWorkspaceRelativePath, resolveWorkspaceRoot, WorkspacePolicyError } from '../workspacePolicy';
import { parseNewAdrFormMessage, type EditValues, type FormValues, type ListState, visualFormInputLimit, VisualCreationSubmission } from '../webview/newAdrFormMessages';
import { LatestListRequest } from '../webview/adrListModel';
import { applyConfigurationWrites, configurationWrites, type ConfigurationWrite, type VisualSettingsValues } from '../webview/settingsModel';
import { buildVisualEditArguments } from './editArguments';
import type { McpDiscoveryResult } from '../mcp/discovery';
import type { McpSettingsStatus } from '../webview/settingsModel';
import { parseFeatureMessage, type FeatureValues, type VisualFeature } from '../webview/featureMessages';
import { VisualFeatureExecutor } from './visualFeatures';

interface NewAdrFormDependencies {
  readonly executor: CliExecutor;
  readonly log: OperationalLog;
  readonly extensionUri: vscode.Uri;
  readonly explorer: AdrExplorerProvider;
  readonly refreshExplorer: () => void;
  readonly initializeRepository: (folder: vscode.WorkspaceFolder) => Promise<void>;
  readonly checkInstallation: (folder: vscode.WorkspaceFolder, showNotification?: boolean) => Promise<InstallationCheckResult | undefined>;
  readonly checkMcpConfiguration: (
    folder: vscode.WorkspaceFolder,
    enabled: boolean,
    serverPath: string,
    signal?: AbortSignal,
  ) => Promise<McpDiscoveryResult>;
}

type InitialScreen = 'list' | 'form';
type VisualScreen = InitialScreen | 'edit' | 'settings' | 'features' | 'feature';

export class NewAdrFormPanel implements vscode.Disposable {
  private static current: NewAdrFormPanel | undefined;
  private readonly panel: vscode.WebviewPanel;
  private readonly disposables: vscode.Disposable[] = [];
  private activeOperation: AbortController | undefined;
  private readonly submission = new VisualCreationSubmission();
  private disposed = false;
  private selectedWorkspace: string | undefined;
  private readonly listRequests = new LatestListRequest();
  private listActionActive = false;
  private settingsCheckActive = false;
  private mcpCheckActive = false;
  private readonly featureExecutor: VisualFeatureExecutor;
  private featurePromptActive = false;
  private created: { readonly workspace: string; readonly relativePath: string; readonly filePath: string; readonly kind: 'created' | 'updated' } | undefined;

  private constructor(private readonly dependencies: NewAdrFormDependencies, private screen: VisualScreen) {
    this.featureExecutor = new VisualFeatureExecutor(dependencies.executor);
    this.panel = vscode.window.createWebviewPanel('adrGuard.visual', vscode.l10n.t('ADR Guard'), vscode.ViewColumn.Active, {
      enableScripts: true,
      retainContextWhenHidden: true,
      localResourceRoots: [vscode.Uri.joinPath(dependencies.extensionUri, 'media')],
    });
    this.panel.webview.html = this.html();
    this.disposables.push(
      this.panel,
      this.panel.onDidDispose(() => this.dispose()),
      this.panel.webview.onDidReceiveMessage(async (message: unknown) => this.receive(message)),
      dependencies.explorer.onDidChangeCatalog(() => {
        if (this.screen === 'list' && this.selectedWorkspace !== undefined) void this.loadList(this.selectedWorkspace);
      }),
    );
  }

  public static async show(dependencies: NewAdrFormDependencies, screen: InitialScreen = 'form'): Promise<void> {
    if (this.current !== undefined && !this.current.disposed) {
      this.current.panel.reveal(vscode.ViewColumn.Active);
      await this.current.navigate(screen);
      return;
    }
    this.current = new NewAdrFormPanel(dependencies, screen);
  }

  public dispose(): void {
    if (this.disposed) return;
    this.disposed = true;
    this.listRequests.invalidate();
    this.activeOperation?.abort();
    if (NewAdrFormPanel.current === this) NewAdrFormPanel.current = undefined;
    for (const disposable of this.disposables.splice(0)) disposable.dispose();
  }

  private async receive(value: unknown): Promise<void> {
    const featureMessage = parseFeatureMessage(value);
    if (featureMessage !== undefined) {
      await this.receiveFeature(featureMessage);
      return;
    }
    const inputLimit = visualFormInputLimit(value);
    if (inputLimit !== undefined) {
      await this.post({ type: 'error', message: inputLimit === 'edit'
        ? vscode.l10n.t('This ADR is too large for visual editing. Reduce the edited content or use the Markdown editor.')
        : vscode.l10n.t('This ADR draft is too large for the visual form. Reduce the field content or use traditional creation.') });
      return;
    }
    const message = parseNewAdrFormMessage(value);
    if (message === undefined || this.disposed) {
      if (!this.disposed) await this.post({ type: 'error', message: vscode.l10n.t('The form sent an invalid message.') });
      return;
    }
    if (message.type === 'ready') return this.initialize();
    if (message.type === 'openSettings') {
      if ((this.screen !== 'list' && this.screen !== 'settings') || this.submission.isActive || message.workspace === undefined) return;
      this.selectedWorkspace = message.workspace;
      this.screen = 'settings';
      await this.post({ type: 'showSettings', settings: await this.settingsFor(workspaceFolder(message.workspace)) });
      return;
    }
    if (message.type === 'cancelSettings') {
      if (this.screen !== 'settings' || this.submission.isActive || this.settingsCheckActive || this.mcpCheckActive) return;
      this.screen = 'list';
      if (message.workspace !== undefined) this.selectedWorkspace = message.workspace;
      await this.post({ type: 'showList', ...(message.state === undefined ? {} : { state: message.state }) });
      if (this.selectedWorkspace !== undefined) await this.loadList(this.selectedWorkspace, message.state);
      return;
    }
    if (message.type === 'saveSettings') {
      if (this.screen !== 'settings' || message.settings === undefined || !this.submission.begin()) return;
      await this.saveSettings(message.settings, message.state);
      return;
    }
    if (message.type === 'checkSettingsInstallation') {
      if (message.workspace === undefined || this.settingsCheckActive || this.screen !== 'settings') return;
      await this.checkSettingsInstallation(message.workspace);
      return;
    }
    if (message.type === 'checkMcpConfiguration') {
      if (message.workspace === undefined || this.mcpCheckActive || this.screen !== 'settings') return;
      if (message.mcpEnabled === undefined || message.mcpPath === undefined) return;
      await this.checkMcpConfiguration(message.workspace, message.mcpEnabled, message.mcpPath);
      return;
    }
    if (message.type === 'openMcpDocumentation') {
      await vscode.env.openExternal(vscode.Uri.parse('https://code.visualstudio.com/docs/copilot/chat/mcp-servers'));
      return;
    }
    if (message.type === 'newAdr') {
      if (this.submission.isActive) return;
      this.selectedWorkspace = message.workspace;
      this.screen = 'form';
      await this.post({ type: 'showForm', workspace: message.workspace });
      return;
    }
    if (message.type === 'showList' || message.type === 'cancel') {
      if (this.submission.isActive) return;
      this.activeOperation?.abort();
      this.screen = 'list';
      if (message.workspace !== undefined) this.selectedWorkspace = message.workspace;
      await this.post({ type: 'showList', ...(message.state === undefined ? {} : { state: message.state }) });
      if (this.selectedWorkspace !== undefined) await this.loadList(this.selectedWorkspace, message.state);
      return;
    }
    if (message.type === 'selectWorkspace' || message.type === 'refreshList') {
      if (this.screen !== 'list' || message.workspace === undefined) return;
      this.selectedWorkspace = message.workspace;
      await this.loadList(message.workspace, message.state);
      return;
    }
    if (message.type === 'initializeRepository' || message.type === 'checkInstallation') {
      if (this.screen !== 'list' || message.workspace === undefined || message.workspace !== this.selectedWorkspace || this.listActionActive) return;
      await this.runListAction(message.type, message.workspace, message.state);
      return;
    }
    if (message.type === 'openAdr') {
      if (this.screen !== 'list' || message.workspace === undefined || message.relativePath === undefined) return;
      return this.openAdr(message.workspace, message.relativePath);
    }
    if (message.type === 'editAdr') {
      if (this.screen !== 'list' || message.workspace === undefined || message.relativePath === undefined) return;
      this.selectedWorkspace = message.workspace;
      this.screen = 'edit';
      await this.loadEdit(message.workspace, message.relativePath);
      return;
    }
    if (message.type === 'saveEdit') {
      if (this.screen !== 'edit' || message.editValues === undefined || !this.submission.begin()) return;
      await this.saveEdit(message.editValues);
      return;
    }
    if (message.type === 'revealCreated') {
      if (this.screen !== 'list' || message.workspace === undefined) return;
      await this.post({ type: 'clearListFilters', workspace: message.workspace });
      await this.loadList(message.workspace);
      return;
    }
    if (this.screen !== 'form' || !this.submission.accepts()) return;
    if (message.type === 'traditional') {
      await vscode.commands.executeCommand('adrGuard.createAdr');
      return;
    }
    if (message.values === undefined) return;
    this.selectedWorkspace = message.values.workspace;
    if (message.type === 'create' && !this.submission.begin()) return;
    await this.run(message.type, message.values);
  }

  private async receiveFeature(message: NonNullable<ReturnType<typeof parseFeatureMessage>>): Promise<void> {
    if (this.disposed) return;
    if (message.type === 'cancelFeatureOperation' && message.workspace === this.selectedWorkspace) {
      this.activeOperation?.abort();
      return;
    }
    if (this.submission.isActive) return;
    if (message.type === 'openFeatures' && message.workspace !== undefined) {
      this.selectedWorkspace = message.workspace;
      this.screen = 'features';
      await this.post({ type: 'showFeatures', workspace: message.workspace });
      return;
    }
    if (message.type === 'showFeature' && message.workspace !== undefined && message.feature !== undefined) {
      this.selectedWorkspace = message.workspace;
      this.screen = 'feature';
      await this.showFeature(message.workspace, message.feature, message.resource);
      return;
    }
    if (message.type === 'cancelFeature' && message.workspace !== undefined) {
      this.activeOperation?.abort();
      this.screen = 'features';
      await this.post({ type: 'showFeatures', workspace: message.workspace });
      return;
    }
    if (message.type === 'openFeatureResource' && message.workspace !== undefined && message.resource !== undefined) {
      await this.openFeatureResource(message.workspace, message.resource);
      return;
    }
    if ((message.type === 'executeFeature' || message.type === 'createDraft') && message.values !== undefined) {
      await this.executeFeature(message.values, message.type === 'createDraft');
    }
  }

  private async showFeature(workspace: string, feature: VisualFeature, selectedAdr?: string): Promise<void> {
    const folder = workspaceFolder(workspace);
    const validation = readValidationConfiguration(folder.uri);
    const configuration = readFeatureConfiguration(folder.uri);
    let adrs: readonly { readonly relativePath: string; readonly title?: string; readonly id?: string }[] = [];
    if (feature === 'review' || feature === 'drift') {
      adrs = (await this.dependencies.explorer.catalogFor(folder)).map((entry) => ({
        relativePath: path.posix.join(validation.directory.replaceAll('\\', '/'), entry.relativePath),
        ...(entry.metadata.title ? { title: entry.metadata.title } : {}),
        ...(entry.metadata.id ? { id: entry.metadata.id } : {}),
      }));
    }
    await this.post({ type: 'showFeature', feature, workspace, defaults: { ...configuration, directory: validation.directory, baseReference: validation.baseReference }, adrs, ...(selectedAdr === undefined ? {} : { selectedAdr }) });
  }

  private async executeFeature(values: FeatureValues, createDraft: boolean): Promise<void> {
    if (this.featurePromptActive) return;
    this.featurePromptActive = true;
    let confirmation: boolean;
    try { confirmation = await this.confirmFeatureExecution(values, createDraft); }
    finally { this.featurePromptActive = false; }
    if (!confirmation) return;
    if (!this.submission.begin()) return;
    const controller = new AbortController();
    this.activeOperation?.abort();
    this.activeOperation = controller;
    await this.post({ type: 'featureBusy', feature: values.feature });
    try {
      const result = await this.featureExecutor.run(workspaceFolder(values.workspace), values, controller.signal, createDraft);
      if (values.feature === 'catalog' || values.feature === 'baseline' || (values.feature === 'draft' && createDraft)) this.dependencies.refreshExplorer();
      await this.post({ type: 'featureResult', workspace: values.workspace, result });
    } catch (error: unknown) {
      if (controller.signal.aborted) return;
      this.dependencies.log.info(`Visual feature '${values.feature}' failed: ${safeLogError(error)}`);
      await this.post({ type: 'featureError', message: featureErrorMessage(error) });
    } finally {
      if (this.activeOperation === controller) this.activeOperation = undefined;
      this.submission.end();
      if (!this.disposed) await this.post({ type: 'featureIdle', feature: values.feature });
    }
  }

  private async confirmFeatureExecution(values: FeatureValues, createDraft: boolean): Promise<boolean> {
    if (values.feature === 'impact' || values.feature === 'drift') return true;
    let message: string;
    if (values.feature === 'catalog') message = vscode.l10n.t('ADR Guard will update the ADR directory README using the selected catalog. Continue?');
    else if (values.feature === 'baseline') message = values.update
      ? vscode.l10n.t('ADR Guard will replace the selected diagnostic baseline. Continue?')
      : vscode.l10n.t('ADR Guard will create the selected diagnostic baseline. Continue?');
    else if (values.feature === 'review') message = vscode.l10n.t('Send the selected ADR and explicitly selected context to {0} using model {1}? The provider is external and the review remains advisory.', values.provider ?? '', values.model ?? '');
    else message = createDraft
      ? vscode.l10n.t('Generate the ADR again with {0}/{1} and create a Proposed document? This is a new provider invocation and may differ from the preview.', values.provider ?? '', values.model ?? '')
      : vscode.l10n.t('Send the entered context and explicitly selected files to {0} using model {1} for a draft preview?', values.provider ?? '', values.model ?? '');
    const proceed = vscode.l10n.t('Continue');
    return await vscode.window.showWarningMessage(message, { modal: true }, proceed) === proceed;
  }

  private async openFeatureResource(workspace: string, resource: string): Promise<void> {
    try {
      const folder = workspaceFolder(workspace);
      const root = await resolveWorkspaceRoot(asLocation(folder));
      const safePath = await ensureResourceWithinWorkspace(resolveWorkspaceRelativePath(root, resource), root);
      await vscode.window.showTextDocument(vscode.Uri.file(safePath), { preview: true });
    } catch (error: unknown) {
      this.dependencies.log.info(`Feature resource navigation was blocked: ${safeLogError(error)}`);
      await this.post({ type: 'featureError', message: vscode.l10n.t('The selected file could not be opened safely inside this workspace.') });
    }
  }

  private async initialize(): Promise<void> {
    const folders = localFolders();
    const activeFolder = vscode.window.activeTextEditor === undefined ? undefined : vscode.workspace.getWorkspaceFolder(vscode.window.activeTextEditor.document.uri);
    const initial = this.selectedWorkspace === undefined
      ? (activeFolder?.uri.scheme === 'file' ? activeFolder : folders[0])
      : folders.find((folder) => folder.uri.toString() === this.selectedWorkspace);
    this.selectedWorkspace = initial?.uri.toString();
    await this.post({
      type: 'initialize',
      trusted: vscode.workspace.isTrusted,
      workspaces: folders.map((folder) => ({
        id: folder.uri.toString(), name: folder.name,
        directory: readValidationConfiguration(folder.uri).directory,
        ...readCreationConfiguration(folder.uri),
      })),
      initialWorkspace: this.selectedWorkspace,
      initialInterfacePreference: readUiConfiguration().language,
      editorInterfaceLanguage: vscode.env.language.toLowerCase() === 'pt-br' ? 'pt-BR' : 'en-US',
      hostPlatform: process.platform,
      initialScreen: this.screen,
      strings: webviewStrings(this.dependencies.extensionUri),
    });
    if (this.screen === 'list' && this.selectedWorkspace !== undefined && vscode.workspace.isTrusted) await this.loadList(this.selectedWorkspace);
  }

  private async navigate(screen: InitialScreen): Promise<void> {
    if (screen === 'form' && this.screen === 'list') {
      this.screen = 'form';
      await this.post({ type: 'showForm', workspace: this.selectedWorkspace });
    } else if (screen === 'form') {
      await this.post({ type: 'requestShowForm' });
    } else if (this.screen === 'list') {
      await this.post({ type: 'showList' });
      if (this.selectedWorkspace !== undefined) await this.loadList(this.selectedWorkspace);
    } else {
      await this.post({ type: 'requestShowList' });
    }
  }

  private async settingsFor(folder: vscode.WorkspaceFolder): Promise<VisualSettingsValues> {
    const validation = readValidationConfiguration(folder.uri);
    const creation = readCreationConfiguration(folder.uri);
    const cli = readConfiguration();
    const mcp = readMcpConfiguration(folder.uri);
    const features = readFeatureConfiguration(folder.uri);
    const mcpStatus = await this.mcpStatus(folder, mcp.enabled);
    return {
      workspace: folder.uri.toString(), uiLanguage: readUiConfiguration().language,
      documentLanguage: creation.documentLanguage, directory: validation.directory,
      adrFormat: validation.adrFormat, defaultTemplate: creation.defaultTemplate,
      onSave: validation.onSave, debounceMilliseconds: validation.debounceMilliseconds,
      baseline: validation.baseline, baseReference: validation.baseReference,
      cliPath: cli.executablePath ?? '', timeoutMilliseconds: cli.timeoutMilliseconds,
      maxOutputBytes: cli.maxOutputBytes,
      mcpEnabled: mcp.enabled, mcpPath: mcp.serverPath ?? '', mcpStatus,
      impactManifest: features.impactManifest, driftRules: features.driftRules,
      driftBaseline: features.driftBaseline, driftPolicy: features.driftPolicy, catalog: features.catalog,
      aiProvider: features.provider, aiModel: features.model, aiEndpoint: features.endpoint,
    };
  }

  private async saveSettings(values: VisualSettingsValues, state?: ListState): Promise<void> {
    await this.post({ type: 'settingsBusy' });
    let pathSetting: 'directory' | 'baseline' | 'analysis' = 'directory';
    try {
      if (!vscode.workspace.isTrusted) throw new WorkspacePolicyError('Workspace is not trusted.', 'missing');
      const normalized = {
        ...values,
        directory: values.directory.trim(), baseline: values.baseline.trim(),
        baseReference: values.baseReference.trim(), cliPath: values.cliPath.trim(),
        mcpPath: values.mcpPath.trim(),
        impactManifest: values.impactManifest.trim(), driftRules: values.driftRules.trim(),
        driftBaseline: values.driftBaseline.trim(), aiModel: values.aiModel.trim(), aiEndpoint: values.aiEndpoint.trim(),
      };
      const folder = workspaceFolder(values.workspace);
      const root = await resolveWorkspaceRoot(asLocation(folder));
      await ensurePotentialDirectoryWithinWorkspace(resolveWorkspaceRelativePath(root, normalized.directory), root);
      pathSetting = 'baseline';
      await ensurePotentialFileWithinWorkspace(resolveWorkspaceRelativePath(root, normalized.baseline), root);
      pathSetting = 'analysis';
      await ensurePotentialFileWithinWorkspace(resolveWorkspaceRelativePath(root, normalized.impactManifest), root);
      await ensurePotentialFileWithinWorkspace(resolveWorkspaceRelativePath(root, normalized.driftRules), root);
      if (normalized.driftBaseline) await ensurePotentialFileWithinWorkspace(resolveWorkspaceRelativePath(root, normalized.driftBaseline), root);
      await applyConfigurationWrites(
        configurationWrites(normalized),
        (write) => this.configurationValue(write, folder),
        (write, value) => this.updateConfiguration(write, value, folder),
      );
      this.selectedWorkspace = values.workspace;
      this.screen = 'list';
      this.dependencies.refreshExplorer();
      await this.post({ type: 'settingsSaved', settings: normalized });
      await this.post({ type: 'showList', ...(state === undefined ? {} : { state }) });
      await this.loadList(values.workspace, state);
    } catch (error: unknown) {
      this.dependencies.log.info(`Visual settings could not be saved: ${safeLogError(error)}`);
      await this.post({
        type: 'settingsError',
        message: error instanceof WorkspacePolicyError
          ? pathSetting === 'baseline'
            ? vscode.l10n.t('The baseline must be a safe relative JSON file inside the selected workspace.')
            : pathSetting === 'analysis'
              ? vscode.l10n.t('Architecture analysis files must be safe relative JSON paths inside the selected workspace.')
              : vscode.l10n.t('The ADR directory must be a safe relative path inside the selected workspace.')
          : vscode.l10n.t('Settings could not be saved completely. Review the values and try again.'),
      });
    } finally {
      this.submission.end();
      if (!this.disposed) await this.post({ type: 'settingsIdle' });
    }
  }

  private configurationValue(write: ConfigurationWrite, folder: vscode.WorkspaceFolder): unknown {
    const scope = write.target === 'workspaceFolder' ? folder.uri : undefined;
    const inspected = vscode.workspace.getConfiguration(write.section, scope).inspect(write.key);
    return write.target === 'global' ? inspected?.globalValue : inspected?.workspaceFolderValue;
  }

  private updateConfiguration(write: ConfigurationWrite, value: unknown, folder: vscode.WorkspaceFolder): Thenable<void> {
    const scope = write.target === 'workspaceFolder' ? folder.uri : undefined;
    const target = write.target === 'global'
      ? vscode.ConfigurationTarget.Global
      : vscode.ConfigurationTarget.WorkspaceFolder;
    return vscode.workspace.getConfiguration(write.section, scope).update(write.key, value, target);
  }

  private async checkSettingsInstallation(workspace: string): Promise<void> {
    this.settingsCheckActive = true;
    await this.post({ type: 'installationChecking' });
    try {
      const result = await this.dependencies.checkInstallation(workspaceFolder(workspace), false);
      if (result !== undefined) await this.post({ type: 'installationResult', result });
    } catch (error: unknown) {
      this.dependencies.log.info(`Settings installation check failed: ${safeLogError(error)}`);
      await this.post({
        type: 'installationResult',
        result: { available: false, message: vscode.l10n.t('ADR Guard installation check failed for an unknown reason.') },
      });
    } finally {
      this.settingsCheckActive = false;
      if (!this.disposed) await this.post({ type: 'installationCheckIdle' });
    }
  }

  private async checkMcpConfiguration(
    workspace: string,
    enabled: boolean,
    serverPath: string,
  ): Promise<void> {
    this.mcpCheckActive = true;
    const controller = new AbortController();
    this.activeOperation = controller;
    await this.post({ type: 'mcpChecking' });
    try {
      const result = await this.dependencies.checkMcpConfiguration(
        workspaceFolder(workspace),
        enabled,
        serverPath.trim(),
        controller.signal,
      );
      await this.post({ type: 'mcpResult', status: mapMcpStatus(result) });
    } catch (error: unknown) {
      this.dependencies.log.info(`MCP configuration check failed: ${safeLogError(error)}`);
      await this.post({ type: 'mcpResult', status: 'error' });
    } finally {
      if (this.activeOperation === controller) this.activeOperation = undefined;
      this.mcpCheckActive = false;
      if (!this.disposed) await this.post({ type: 'mcpCheckIdle' });
    }
  }

  private async mcpStatus(folder: vscode.WorkspaceFolder, enabled: boolean): Promise<McpSettingsStatus> {
    if (!enabled) return 'disabled';
    try {
      const configuration = readMcpConfiguration(folder.uri);
      return mapMcpStatus(await this.dependencies.checkMcpConfiguration(folder, enabled, configuration.serverPath ?? ''));
    } catch (error: unknown) {
      this.dependencies.log.info(`MCP availability could not be inspected: ${safeLogError(error)}`);
      return 'error';
    }
  }

  private async loadList(workspace: string, state?: ListState): Promise<void> {
    const sequence = this.listRequests.begin();
    await this.post({ type: 'listLoading', workspace });
    try {
      if (!vscode.workspace.isTrusted) throw new WorkspacePolicyError('Workspace is not trusted.', 'missing');
      const folder = workspaceFolder(workspace);
      const configuration = readValidationConfiguration(folder.uri);
      const entries = await this.dependencies.explorer.catalogFor(folder);
      if (!this.listRequests.isCurrent(sequence) || this.disposed || this.screen !== 'list' || this.selectedWorkspace !== workspace) return;
      await this.post({
        type: 'listResult', workspace, directory: configuration.directory,
        entries: entries.map((entry) => ({
          relativePath: entry.relativePath, id: entry.metadata.id, title: entry.metadata.title,
          status: entry.metadata.status, date: entry.metadata.date, warning: entry.metadata.warning,
        })),
        ...(state === undefined ? {} : { state }),
        createdPath: this.created?.workspace === workspace ? this.created.relativePath : undefined,
        highlightKind: this.created?.workspace === workspace ? this.created.kind : undefined,
      });
    } catch (error: unknown) {
      if (!this.listRequests.isCurrent(sequence) || this.disposed || this.screen !== 'list') return;
      this.dependencies.log.info(`ADR list could not load: ${safeLogError(error)}`);
      const code = isNodeError(error) ? error.code : undefined;
      const folder = vscode.workspace.workspaceFolders?.find((candidate) => candidate.uri.toString() === workspace);
      const directory = folder === undefined ? undefined : readValidationConfiguration(folder.uri).directory;
      await this.post({ type: 'listError', workspace, directory, kind: code === 'ENOENT' ? 'missing-directory' : error instanceof WorkspacePolicyError ? 'workspace' : 'read' });
    }
  }

  private async openAdr(workspace: string, relativePath: string): Promise<void> {
    try {
      const target = await this.resolveCatalogAdr(workspace, relativePath);
      await vscode.window.showTextDocument(vscode.Uri.file(target.filePath), { preview: true });
    } catch (error: unknown) {
      this.dependencies.log.info(`ADR list navigation was blocked: ${safeLogError(error)}`);
      await this.post({ type: 'listNavigationError', message: vscode.l10n.t('The selected ADR could not be opened safely.') });
    }
  }

  private async runListAction(
    action: 'initializeRepository' | 'checkInstallation',
    workspace: string,
    state?: ListState,
  ): Promise<void> {
    this.listActionActive = true;
    try {
      const folder = workspaceFolder(workspace);
      if (action === 'checkInstallation') {
        await this.dependencies.checkInstallation(folder);
        return;
      }
      let entryCount = 0;
      try {
        entryCount = (await this.dependencies.explorer.catalogFor(folder)).length;
      } catch (error: unknown) {
        if (!isNodeError(error) || error.code !== 'ENOENT') throw error;
      }
      if (entryCount > 0) {
        await vscode.window.showInformationMessage(vscode.l10n.t('The ADR repository is already initialized.'));
        await this.loadList(workspace, state);
        return;
      }
      await this.dependencies.initializeRepository(folder);
      this.dependencies.refreshExplorer();
      await this.loadList(workspace, state);
      this.panel.reveal(vscode.ViewColumn.Active, true);
    } catch (error: unknown) {
      this.dependencies.log.info(`ADR list action failed: ${safeLogError(error)}`);
      await this.post({ type: 'listNavigationError', message: formErrorMessage(error) });
    } finally {
      this.listActionActive = false;
    }
  }

  private async loadEdit(workspace: string, relativePath: string): Promise<void> {
    this.activeOperation?.abort();
    const controller = new AbortController();
    this.activeOperation = controller;
    await this.post({ type: 'editLoading' });
    try {
      const target = await this.resolveCatalogAdr(workspace, relativePath);
      const args = await this.visualEditArguments(target, controller.signal);
      const execution = await this.dependencies.executor.execute(
        target.folder,
        args,
        controller.signal,
        JSON.stringify({ schemaVersion: editFormSchemaVersion, operation: 'load' }),
      );
      requireSuccessfulResult(execution.result);
      const response = parseEditFormResponse(execution.result.stdout.trim());
      if (response.operation !== 'load' || !samePath(response.path, target.filePath)) {
        throw new Error(vscode.l10n.t('The CLI returned an invalid ADR edit target.'));
      }
      await this.post({ type: 'editLoaded', workspace, relativePath, response });
    } catch (error: unknown) {
      if (controller.signal.aborted) return;
      this.dependencies.log.info(`Visual ADR edit load failed: ${safeLogError(error)}`);
      await this.post({ type: 'editError', message: editErrorMessage(error) });
    } finally {
      if (this.activeOperation === controller) this.activeOperation = undefined;
    }
  }

  private async saveEdit(values: EditValues): Promise<void> {
    this.activeOperation?.abort();
    const controller = new AbortController();
    this.activeOperation = controller;
    await this.post({ type: 'editBusy' });
    try {
      const target = await this.resolveCatalogAdr(values.workspace, values.relativePath);
      const args = await this.visualEditArguments(target, controller.signal);
      const execution = await this.dependencies.executor.execute(
        target.folder,
        args,
        controller.signal,
        JSON.stringify({
          schemaVersion: editFormSchemaVersion,
          operation: 'save',
          fingerprint: values.fingerprint,
          title: values.title,
          fields: values.fields,
        }),
      );
      requireSuccessfulResult(execution.result);
      const response = parseEditFormResponse(execution.result.stdout.trim());
      if (response.operation !== 'save') {
        throw new Error(vscode.l10n.t('The CLI did not confirm the updated ADR.'));
      }
      this.created = { workspace: values.workspace, relativePath: values.relativePath, filePath: target.filePath, kind: 'updated' };
      this.screen = 'list';
      this.dependencies.refreshExplorer();
      await this.post({ type: 'edited', workspace: values.workspace, relativePath: values.relativePath });
      await this.loadList(values.workspace);
      this.panel.reveal(vscode.ViewColumn.Active, true);
      const open = vscode.l10n.t('Open Markdown');
      const selection = await vscode.window.showInformationMessage(vscode.l10n.t('ADR Guard updated {0}.', path.basename(target.filePath)), open);
      if (selection === open) await vscode.window.showTextDocument(vscode.Uri.file(target.filePath), { preview: true });
    } catch (error: unknown) {
      if (controller.signal.aborted) return;
      this.dependencies.log.info(`Visual ADR edit save failed: ${safeLogError(error)}`);
      await this.post({ type: 'editError', message: editErrorMessage(error) });
    } finally {
      if (this.activeOperation === controller) this.activeOperation = undefined;
      this.submission.end();
      if (!this.disposed) await this.post({ type: 'editIdle' });
    }
  }

  private async resolveCatalogAdr(workspace: string, relativePath: string): Promise<{
    readonly folder: vscode.WorkspaceFolder;
    readonly directory: string;
    readonly filePath: string;
    readonly format: 'canonical' | 'madr-4';
  }> {
    const folder = workspaceFolder(workspace);
    const root = await resolveWorkspaceRoot(asLocation(folder));
    const configuration = readValidationConfiguration(folder.uri);
    const directory = await ensureResourceWithinWorkspace(resolveWorkspaceRelativePath(root, configuration.directory), root);
    const candidate = await ensureResourceWithinWorkspace(path.resolve(directory, relativePath), root);
    if (!isResolvedPathWithin(candidate, directory)) throw new WorkspacePolicyError('ADR is outside configured directory.', 'outside-workspace');
    const entries = await this.dependencies.explorer.catalogFor(folder);
    if (!entries.some((entry) => samePath(entry.filePath, candidate))) throw new WorkspacePolicyError('ADR is not in the current catalog.', 'outside-workspace');
    return { folder, directory, filePath: candidate, format: configuration.adrFormat };
  }

  private async visualEditArguments(
    target: Awaited<ReturnType<NewAdrFormPanel['resolveCatalogAdr']>>,
    signal: AbortSignal,
  ): Promise<string[]> {
    const help = await this.dependencies.executor.execute(target.folder, ['edit', '--help'], signal);
    requireSuccessfulResult(help.result);
    return buildVisualEditArguments(target.filePath, target.directory, target.format, help.result.stdout);
  }

  private async run(operation: NewAdrFormOperation, values: FormValues): Promise<void> {
    this.activeOperation?.abort();
    const controller = new AbortController();
    this.activeOperation = controller;
    await this.post({ type: 'busy', operation });
    try {
      const folder = workspaceFolder(values.workspace);
      const root = await resolveWorkspaceRoot(asLocation(folder));
      const target = await ensureResourceWithinWorkspace(resolveWorkspaceRelativePath(root, values.directory), root);
      const args = buildFormArguments(target, values.template, values.culture);
      const input = JSON.stringify({ schemaVersion: newAdrFormSchemaVersion, operation, ...(operation === 'describe' ? {} : { title: values.title, fields: values.fields }) });
      const execution = await this.dependencies.executor.execute(folder, args, controller.signal, input);
      if (execution.result.termination === 'exited' && execution.result.exitCode === 2) throw new FormCapabilityError(safeErrorLine(execution.result.stderr));
      requireSuccessfulResult(execution.result);
      const response = parseNewAdrFormResponse(execution.result.stdout.trim());
      validateResponseForOperation(response, operation);
      if (operation === 'create') {
        const reported = response.path;
        if (reported === undefined || !response.written) throw new Error(vscode.l10n.t('The CLI did not confirm the created ADR.'));
        const safePath = await ensureResourceWithinWorkspace(path.resolve(root, reported), root);
        if (!isResolvedPathWithin(safePath, target)) throw new Error(vscode.l10n.t('The CLI reported an unsafe or unexpected created file path.'));
        this.created = { workspace: values.workspace, relativePath: path.relative(target, safePath).replaceAll(path.sep, '/'), filePath: safePath, kind: 'created' };
        this.selectedWorkspace = values.workspace;
        this.screen = 'list';
        this.dependencies.refreshExplorer();
        await this.post({ type: 'created', workspace: values.workspace, relativePath: this.created.relativePath });
        await this.loadList(values.workspace);
        this.panel.reveal(vscode.ViewColumn.Active, true);
        const open = vscode.l10n.t('Open Markdown');
        const selection = await vscode.window.showInformationMessage(vscode.l10n.t('ADR Guard created {0}.', path.basename(safePath)), open);
        if (selection === open) await vscode.window.showTextDocument(vscode.Uri.file(safePath), { preview: true });
        return;
      }
      await this.post(operation === 'describe'
        ? { type: 'templateLoaded', response }
        : { type: 'result', response });
    } catch (error: unknown) {
      if (controller.signal.aborted) return;
      this.dependencies.log.info(`Visual ADR form operation failed: ${safeLogError(error)}`);
      await this.post({ type: 'error', message: formErrorMessage(error), traditionalAvailable: error instanceof FormCapabilityError || error instanceof CliDiscoveryError });
    } finally {
      if (this.activeOperation === controller) this.activeOperation = undefined;
      if (operation === 'create') this.submission.end();
      if (!this.disposed) await this.post({ type: 'idle', operation });
    }
  }

  private async post(message: unknown): Promise<void> { if (!this.disposed) await this.panel.webview.postMessage(message); }

  private html(): string {
    const nonce = randomBytes(16).toString('base64');
    const script = this.panel.webview.asWebviewUri(vscode.Uri.joinPath(this.dependencies.extensionUri, 'media', 'newAdrForm.js'));
    const style = this.panel.webview.asWebviewUri(vscode.Uri.joinPath(this.dependencies.extensionUri, 'media', 'newAdrForm.css'));
    return `<!DOCTYPE html><html lang="${escapeAttribute(vscode.env.language)}"><head><meta charset="UTF-8"><meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src ${this.panel.webview.cspSource}; script-src 'nonce-${nonce}';"><meta name="viewport" content="width=device-width, initial-scale=1.0"><link rel="stylesheet" href="${style.toString()}"><title>${escapeHtml(vscode.l10n.t('ADR Guard'))}</title></head><body><main id="app" aria-live="polite"></main><script nonce="${nonce}" src="${script.toString()}"></script></body></html>`;
  }
}

class FormCapabilityError extends Error { public constructor(detail?: string) { super(detail); this.name = 'FormCapabilityError'; } }
function localFolders(): vscode.WorkspaceFolder[] { return (vscode.workspace.workspaceFolders ?? []).filter((folder) => folder.uri.scheme === 'file'); }
function buildFormArguments(directory: string, template: FormValues['template'], culture: FormValues['culture']): string[] { return ['new', directory, '--form', ...(template === 'configured' ? [] : ['--template', template]), '--culture', culture]; }
function workspaceFolder(id: string): vscode.WorkspaceFolder { const folder = vscode.workspace.workspaceFolders?.find((candidate) => candidate.uri.toString() === id); if (folder?.uri.scheme !== 'file') throw new WorkspacePolicyError('The selected workspace folder is unavailable.', 'missing'); return folder; }
function validateResponseForOperation(response: NewAdrFormResponse, operation: NewAdrFormOperation): void { if (response.operation !== operation) throw new Error(vscode.l10n.t('The CLI returned a response for a different form operation.')); if (operation === 'preview' && (response.markdown === undefined || response.written)) throw new Error(vscode.l10n.t('The CLI returned an invalid preview response.')); if (operation === 'describe' && (response.path !== undefined || response.markdown !== undefined || response.written)) throw new Error(vscode.l10n.t('The CLI returned an invalid template description.')); }
function formErrorMessage(error: unknown): string { if (error instanceof FormCapabilityError) return vscode.l10n.t('The installed ADR Guard CLI does not support visual creation. Install CLI 1.7.0 or newer, or use the traditional creation flow.'); if (error instanceof WorkspacePolicyError) return error.code === 'outside-workspace' || error.code === 'invalid-path' ? vscode.l10n.t('The ADR destination must be a safe directory inside the selected workspace.') : vscode.l10n.t('Open a trusted local workspace before using the ADR form.'); if (error instanceof CliDiscoveryError) return vscode.l10n.t('ADR Guard was not found. Configure adrGuard.cli.path or install it on the extension host PATH.'); return error instanceof Error ? error.message : vscode.l10n.t('ADR Guard failed for an unknown reason.'); }
function editErrorMessage(error: unknown): string { if (error instanceof WorkspacePolicyError) return vscode.l10n.t('The selected ADR is unavailable or outside the configured ADR directory.'); if (error instanceof CliDiscoveryError) return vscode.l10n.t('ADR Guard was not found. Configure adrGuard.cli.path or install it on the extension host PATH.'); return error instanceof Error ? error.message : vscode.l10n.t('ADR Guard failed for an unknown reason.'); }
function webviewStrings(extensionUri: vscode.Uri): Record<'en-US' | 'pt-BR', Record<string, string>> {
  const values = ['ADR Guard', 'Architecture Decision Records', 'New ADR', 'Edit', 'Edit ADR', 'Save changes', 'Saving changes…', 'Loading ADR…', 'Refresh', 'Retry', 'Search ADRs', 'All statuses', 'Number (descending)', 'Number (ascending)', 'Title (A–Z)', 'Date (newest)', 'Sort by', 'Filter by status', 'No ADRs were found in this directory.', 'Create your first ADR to record an architectural decision.', 'No ADRs match the current search and filters.', 'Clear filters', 'The configured ADR directory does not exist.', 'ADR Guard does not create directories while browsing.', 'Open a local workspace folder to browse ADRs.', 'Trust this workspace to browse ADRs.', 'The ADR directory could not be read.', 'Loading ADRs…', '{0} ADRs shown', '{0} of {1} ADRs shown', 'Unknown title', 'Unknown status', 'No identifier', 'Open Markdown', 'This ADR has incomplete metadata.', 'The new ADR is hidden by the current filters.', 'The updated ADR is hidden by the current filters.', 'Reveal it', 'ADR created.', 'ADR updated.', 'Create ADR with Form', 'Back to ADR list', 'Form sections', 'Document setup', 'Decision content', 'Document metadata', 'Document sections', 'Workspace folder', 'ADR destination directory', 'Creation template', 'Document language', 'Decision title', 'Markdown preview', 'Create ADR', 'Cancel', 'Use traditional creation', 'Configured template', 'Minimal', 'Extended', 'Minimal captures the context, decision, and consequences.', 'Extended adds optional drivers, alternatives, rationale, risks, and references.', 'This custom template cannot be mapped safely to form fields. Use the traditional creation flow to preserve it.', 'Required', 'Optional', 'Preview updates after you edit the form.', 'No file is written until you choose Create ADR.', 'Context', 'Decision drivers', 'Options considered', 'Decision', 'Rationale', 'Consequences', 'Positive consequences', 'Negative consequences', 'Risks', 'References', 'Describe the problem, constraints, and relevant stakeholders.', 'List requirements, quality attributes, constraints, and trade-offs.', 'Compare viable alternatives, including keeping the current approach.', 'State the proposed choice, its scope, and why it addresses the problem.', 'Explain why this option best satisfies the decision drivers.', 'Describe expected impact and trade-offs.', 'Record expected benefits and assumptions.', 'Record costs, limitations, and added complexity.', 'Identify uncertainties, failure modes, mitigations, and owners.', 'Add links to evidence, related documents, or ADRs.', 'Loading template…', 'The template could not be displayed. Try again.', 'Creating ADR…', 'Preview unavailable until required fields are complete.', 'Discard the unsaved form data and return to the ADR list?', 'Discard the unsaved form data and start a new ADR?', 'The selected ADR could not be opened safely.'];
  values.splice(2, 0, 'Interface language', 'Initialize repository', 'Check installation',
    'The ADR repository is already initialized.',
    'Repository initialization is unavailable until the ADR directory can be read.');
  values.push(
    'Settings', 'Configure ADR Guard preferences.', 'Languages', 'Automatic (VS Code)',
    'Default language for new documents', 'Native VS Code commands and controls continue to use the editor language.',
    'Repository', 'Selected workspace', 'ADR directory', 'Use a relative path inside the selected workspace.',
    'ADR format', 'Canonical', 'MADR 4.0', 'The validation format is independent from the creation template.',
    'Document creation', 'Default creation template', 'Configured in the CLI',
    'You can still choose another template for an individual document.', 'Save settings', 'Saving settings…',
    'Discard the unsaved settings and return to the ADR list?', 'Discard the unsaved settings and switch workspace?',
    'Discard the unsaved settings and start a new ADR?', 'Enter a safe relative directory inside the selected workspace.',
    'Validation', 'Automatic validation on save', 'Validation runs only after an applicable ADR file is saved.',
    'Validation interval (milliseconds)', 'Accepted range: 200 to 5000 milliseconds.', 'Baseline file',
    'ADR Guard only reads this existing JSON file during explicit baseline validation.', 'Default Git reference',
    'Incremental validation lets you edit this value and never fetches automatically.', 'CLI integration',
    'ADR Guard executable', 'Leave empty to discover ADR Guard on the extension host PATH.',
    'Checking installation…', 'Installation check failed.', 'The check uses the saved CLI settings.',
    'Advanced settings', 'CLI timeout (milliseconds)', 'Accepted range: 1000 to 120000 milliseconds.',
    'CLI output limit (bytes)', 'Accepted range: 1024 to 1048576 bytes.',
    'Review the validation, CLI, and MCP settings and enter values within the accepted limits.',
    'ADR Guard {0} is available ({1}).', 'configured path',
    'MCP Integration', 'Enable MCP integration',
    'Disabled by default. VS Code starts the server only when an agent requests it.',
    'ADR Guard MCP server',
    'Configure adr-guard-mcp.dll or the native adr-guard-mcp executable on the extension host.',
    'Check MCP configuration', 'VS Code MCP guidance',
    'Availability does not mean that the server is connected or running.',
    'Checking MCP configuration...', 'Disabled', 'Not configured', 'Executable not found',
    'Invalid configuration', 'Available for startup', 'Known error',
    'Architecture and Governance', 'Architecture Impact', 'Architectural Fitness',
    'Diagnostic Baselines', 'ADR Catalog', 'AI-Assisted ADR Draft', 'AI-Assisted Technical Review',
    'Correlate Git changes with explicit ADR-to-code mappings.',
    'Evaluate ADR-backed architecture rules from bounded evidence.',
    'Create or explicitly update a validation baseline.',
    'Generate the standard index or enriched governance catalog.',
    'Generate and review a Proposed ADR preview.', 'Request an advisory review of one existing ADR.',
    'Open', 'Configuration', 'Git comparison reference', 'Impact manifest', 'Architecture rules file',
    'Authorized evidence files (one per line)', 'Optional architecture baseline', 'Policy', 'Advisory', 'Enforce',
    'Baseline destination', 'Replace an existing baseline after confirmation', 'Standard index', 'Enriched catalog',
    'Catalog type', 'ADR to review', 'Optional Git comparison reference', 'Review policy',
    'Optional review policy file', 'Select a provider', 'Provider', 'Model identifier',
    'HTTPS endpoint (OpenAI-compatible only)', 'Explicit context files (one per line)',
    'Include bounded existing ADR context',
    'Only the selected material is sent to the external provider. Credentials stay in the CLI environment and are never shown here.',
    'Generate preview', 'Execute', 'Discard the unsaved feature inputs?', 'Affected decisions', 'Unmapped changes',
    'Unknown mappings', 'Open ADR', 'Pass', 'Violations', 'Unknown', 'Not applicable', 'Review outcome',
    'Compared with Git reference', 'Limitations', 'Generated Markdown preview', 'Create Proposed ADR',
    'Creation invokes the provider again; the created content may differ from this preview.', 'Operation completed.',
    'Open file', 'Running ADR Guard…', 'advisory', 'affected', 'unknown', 'not-matched', 'pass', 'violation',
    'not-applicable', 'no-finding',
    'Architecture Analysis', 'AI Providers', 'Default drift policy', 'Default catalog type',
    'Advisory remains the safe default; enforcement is never enabled automatically.',
    'Preferred provider', 'No provider selected', 'No provider or model is selected automatically.',
    'API keys remain in the CLI environment and are never stored by the extension.',
    'Review ADR', 'View existing baseline', 'Cancel operation', 'Recommendation', 'Investigation priority',
  );
  const english = Object.fromEntries(values.map((value) => [value, value]));
  const portugueseCatalog = readTranslationCatalog(vscode.Uri.joinPath(extensionUri, 'l10n', 'bundle.l10n.pt-br.json'));
  const portuguese = Object.fromEntries(values.map((value) => [value, portugueseCatalog[value] ?? value]));
  return { 'en-US': english, 'pt-BR': portuguese };
}
function readTranslationCatalog(uri: vscode.Uri): Record<string, string> {
  try {
    const value = JSON.parse(readFileSync(uri.fsPath, 'utf8')) as unknown;
    if (typeof value !== 'object' || value === null || Array.isArray(value)) return {};
    return Object.fromEntries(Object.entries(value).filter((entry): entry is [string, string] => typeof entry[1] === 'string'));
  } catch {
    return {};
  }
}
function safeLogError(error: unknown): string { return (error instanceof Error ? error.message : 'unknown error').replace(/[\r\n\0]/gu, ' ').slice(0, 300); }
function escapeHtml(value: string): string { return value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;'); }
function escapeAttribute(value: string): string { return escapeHtml(value).replaceAll('"', '&quot;').replaceAll("'", '&#39;'); }
function samePath(left: string, right: string): boolean { const normalize = (value: string): string => process.platform === 'win32' ? path.resolve(value).toLowerCase() : path.resolve(value); return normalize(left) === normalize(right); }
function isNodeError(error: unknown): error is NodeJS.ErrnoException { return error instanceof Error && 'code' in error; }
function mapMcpStatus(result: McpDiscoveryResult): McpSettingsStatus {
  if (result.code === 'available') return 'available';
  if (result.code === 'not-configured') return result.detail === 'disabled' ? 'disabled' : 'not-configured';
  if (result.code === 'not-found') return 'not-found';
  if (result.code === 'invalid-configuration') return 'invalid-configuration';
  return 'error';
}

function featureErrorMessage(error: unknown): string {
  if (error instanceof WorkspacePolicyError) return vscode.l10n.t('All selected files must be safe resources inside the selected trusted local workspace.');
  if (error instanceof CliDiscoveryError) return vscode.l10n.t('ADR Guard was not found. Configure adrGuard.cli.path or install it on the extension host PATH.');
  return error instanceof Error ? error.message : vscode.l10n.t('ADR Guard failed for an unknown reason.');
}
