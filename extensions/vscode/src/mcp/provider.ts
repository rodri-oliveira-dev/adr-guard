import { createHash } from 'node:crypto';
import path from 'node:path';
import * as vscode from 'vscode';
import { readMcpConfiguration, readValidationConfiguration } from '../configuration';
import { OperationalLog } from '../logging';
import { allWorkspaceRoots, asLocation } from '../workspace';
import { ensureResourceWithinWorkspace, resolveWorkspaceRelativePath, resolveWorkspaceRoot } from '../workspacePolicy';
import { McpServerDiscovery, type McpDiscoveryResult } from './discovery';

export const mcpProviderId = 'adrGuard.mcpServers';

export class AdrGuardMcpProvider implements vscode.McpServerDefinitionProvider<vscode.McpStdioServerDefinition>, vscode.Disposable {
  private readonly changeEmitter = new vscode.EventEmitter<void>();
  private readonly disposables: vscode.Disposable[];
  private disposed = false;
  public readonly onDidChangeMcpServerDefinitions = this.changeEmitter.event;

  public constructor(
    private readonly discovery: McpServerDiscovery,
    private readonly log: OperationalLog,
  ) {
    this.disposables = [
      this.changeEmitter,
      vscode.workspace.onDidChangeConfiguration((event) => {
        if (event.affectsConfiguration('adrGuard.mcp') || event.affectsConfiguration('adrGuard.validation.directory')) {
          this.changeEmitter.fire();
        }
      }),
      vscode.workspace.onDidChangeWorkspaceFolders(() => this.changeEmitter.fire()),
      vscode.workspace.onDidGrantWorkspaceTrust(() => this.changeEmitter.fire()),
    ];
  }

  public async provideMcpServerDefinitions(token: vscode.CancellationToken): Promise<vscode.McpStdioServerDefinition[]> {
    if (this.disposed || token.isCancellationRequested || !vscode.workspace.isTrusted) return [];
    const folders = (vscode.workspace.workspaceFolders ?? []).filter((folder) => folder.uri.scheme === 'file');
    if (folders.length === 0) return [];
    const roots = await allWorkspaceRoots();
    const duplicateNames = new Set(folders.filter((folder, index) =>
      folders.findIndex((candidate) => candidate.name === folder.name) !== index).map((folder) => folder.name));
    const definitions = await Promise.all(folders.map(async (folder) => {
      try {
        const configuration = readMcpConfiguration(folder.uri);
        if (!configuration.enabled || !configuration.serverPath || token.isCancellationRequested) return undefined;
        if (configuration.ignoredWorkspaceServerPath) {
          this.log.info('Ignored a workspace-scoped adrGuard.mcp.path value; only the trusted user/machine value is accepted.');
        }
        const root = await resolveWorkspaceRoot(asLocation(folder));
        const result = await this.discovery.inspect({ configuredPath: configuration.serverPath, workspaceRoots: roots });
        if (result.code !== 'available' || result.launch === undefined || cancellationRequested(token)) {
          this.log.info(`ADR Guard MCP definition unavailable for '${folder.name}': ${result.code}.`);
          return undefined;
        }
        const validation = readValidationConfiguration(folder.uri);
        const adrDirectory = await ensureResourceWithinWorkspace(
          resolveWorkspaceRelativePath(root, validation.directory),
          root,
        );
        const suffix = duplicateNames.has(folder.name)
          ? ` (${stableSuffix(folder.uri.toString())})`
          : '';
        const args = [
          ...result.launch.argsPrefix,
          '--repository', root,
          '--adr-directory', path.relative(root, adrDirectory),
        ];
        const definition = new vscode.McpStdioServerDefinition(
          `ADR Guard - ${folder.name}${suffix}`,
          result.launch.command,
          args,
          {},
          '1.0',
        );
        definition.cwd = vscode.Uri.file(root);
        return definition;
      } catch (error: unknown) {
        this.log.info(`ADR Guard MCP definition unavailable for '${folder.name}': ${errorName(error)}.`);
        return undefined;
      }
    }));
    return definitions.filter((definition): definition is vscode.McpStdioServerDefinition => definition !== undefined);
  }

  public async resolveMcpServerDefinition(
    server: vscode.McpStdioServerDefinition,
    token: vscode.CancellationToken,
  ): Promise<vscode.McpStdioServerDefinition | undefined> {
    if (token.isCancellationRequested || !vscode.workspace.isTrusted) return undefined;
    const definitions = await this.provideMcpServerDefinitions(token);
    return definitions.find((definition) => definition.label === server.label);
  }

  public async inspect(
    folder: vscode.WorkspaceFolder,
    signal?: AbortSignal,
    override?: { readonly enabled: boolean; readonly serverPath?: string },
  ): Promise<McpDiscoveryResult> {
    if (!vscode.workspace.isTrusted || folder.uri.scheme !== 'file') {
      return { code: 'invalid-configuration', detail: 'A trusted local file-system workspace is required.' };
    }
    const configuration = override ?? readMcpConfiguration(folder.uri);
    if (!configuration.enabled) return { code: 'not-configured', detail: 'disabled' };
    return this.discovery.inspect({
      ...(configuration.serverPath ? { configuredPath: configuration.serverPath } : {}),
      workspaceRoots: await allWorkspaceRoots(),
      ...(signal === undefined ? {} : { signal }),
    });
  }

  public dispose(): void {
    if (this.disposed) return;
    this.disposed = true;
    for (const disposable of this.disposables.splice(0)) disposable.dispose();
  }
}

export function registerMcpProvider(
  context: vscode.ExtensionContext,
  log: OperationalLog,
): AdrGuardMcpProvider {
  const provider = new AdrGuardMcpProvider(new McpServerDiscovery(), log);
  context.subscriptions.push(
    provider,
    vscode.lm.registerMcpServerDefinitionProvider(mcpProviderId, provider),
  );
  return provider;
}

function stableSuffix(value: string): string {
  return createHash('sha256').update(value).digest('hex').slice(0, 8);
}

function cancellationRequested(token: vscode.CancellationToken): boolean {
  return token.isCancellationRequested;
}

function errorName(error: unknown): string {
  return error instanceof Error ? error.name : 'UnknownError';
}
