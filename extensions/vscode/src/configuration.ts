import * as vscode from 'vscode';
import { isSafeGitBaseReference, type AdrFormat } from './commands/arguments';
import type { DefaultTemplate, DocumentLanguage, InterfaceLanguagePreference } from './webview/settingsModel';

export interface AdrGuardConfiguration {
  readonly executablePath?: string;
  readonly timeoutMilliseconds: number;
  readonly maxOutputBytes: number;
  readonly ignoredWorkspaceExecutablePath: boolean;
}

export interface ValidationConfiguration {
  readonly directory: string;
  readonly adrFormat: AdrFormat;
  readonly baseline: string;
  readonly onSave: boolean;
  readonly debounceMilliseconds: number;
  readonly baseReference: string;
}

export interface CreationConfiguration {
  readonly documentLanguage: DocumentLanguage;
  readonly defaultTemplate: DefaultTemplate;
}

export interface UiConfiguration {
  readonly language: InterfaceLanguagePreference;
}

export interface McpConfiguration {
  readonly enabled: boolean;
  readonly serverPath?: string;
  readonly ignoredWorkspaceServerPath: boolean;
}

export interface FeatureConfiguration {
  readonly impactManifest: string;
  readonly driftRules: string;
  readonly driftBaseline: string;
  readonly driftPolicy: 'advisory' | 'enforce';
  readonly catalog: 'standard' | 'enriched';
  readonly provider: '' | 'openai' | 'anthropic' | 'gemini' | 'openai-compatible';
  readonly model: string;
  readonly endpoint: string;
}

export function readConfiguration(): AdrGuardConfiguration {
  const configuration = vscode.workspace.getConfiguration('adrGuard.cli');
  const pathInspection = configuration.inspect<string>('path');
  const executablePath = pathInspection?.globalValue?.trim();
  const ignoredWorkspaceExecutablePath =
    pathInspection?.workspaceValue !== undefined || pathInspection?.workspaceFolderValue !== undefined;

  return {
    ...(executablePath ? { executablePath } : {}),
    timeoutMilliseconds: clampInteger(
      configuration.get<number>('timeoutMilliseconds', 15000),
      1000,
      120000,
    ),
    maxOutputBytes: clampInteger(
      configuration.get<number>('maxOutputBytes', 262144),
      1024,
      1048576,
    ),
    ignoredWorkspaceExecutablePath,
  };
}

export function readValidationConfiguration(scope?: vscode.ConfigurationScope): ValidationConfiguration {
  const configuration = vscode.workspace.getConfiguration('adrGuard.validation', scope);
  const baseReference = configuration.get<string>('baseReference', 'main').trim();
  return {
    directory: configuration.get<string>('directory', 'docs/adr').trim() || 'docs/adr',
    adrFormat: configuration.get<AdrFormat>('adrFormat', 'canonical') === 'madr-4' ? 'madr-4' : 'canonical',
    baseline: configuration.get<string>('baseline', '.adrguard-baseline.json').trim() || '.adrguard-baseline.json',
    onSave: configuration.get<boolean>('onSave', false),
    debounceMilliseconds: clampInteger(
      configuration.get<number>('debounceMilliseconds', 750),
      200,
      5000,
    ),
    baseReference: isSafeGitBaseReference(baseReference) ? baseReference : 'main',
  };
}

export function readCreationConfiguration(scope?: vscode.ConfigurationScope): CreationConfiguration {
  const configuration = vscode.workspace.getConfiguration('adrGuard.creation', scope);
  const documentLanguage = configuration.get<string>('documentLanguage', 'en-US');
  const defaultTemplate = configuration.get<string>('defaultTemplate', 'configured');
  return {
    documentLanguage: documentLanguage === 'pt-BR' ? 'pt-BR' : 'en-US',
    defaultTemplate: defaultTemplate === 'minimal' || defaultTemplate === 'extended' ? defaultTemplate : 'configured',
  };
}

export function readUiConfiguration(): UiConfiguration {
  const inspection = vscode.workspace.getConfiguration('adrGuard.ui').inspect<string>('language');
  const language = inspection?.globalValue ?? inspection?.defaultValue ?? 'auto';
  return { language: language === 'en-US' || language === 'pt-BR' ? language : 'auto' };
}

export function readMcpConfiguration(scope?: vscode.ConfigurationScope): McpConfiguration {
  const configuration = vscode.workspace.getConfiguration('adrGuard.mcp', scope);
  const pathInspection = configuration.inspect<string>('path');
  const serverPath = pathInspection?.globalValue?.trim();
  return {
    enabled: configuration.get<boolean>('enabled', false),
    ...(serverPath ? { serverPath } : {}),
    ignoredWorkspaceServerPath:
      pathInspection?.workspaceValue !== undefined || pathInspection?.workspaceFolderValue !== undefined,
  };
}

export function readFeatureConfiguration(scope?: vscode.ConfigurationScope): FeatureConfiguration {
  const analysis = vscode.workspace.getConfiguration('adrGuard.analysis', scope);
  const governance = vscode.workspace.getConfiguration('adrGuard.governance', scope);
  const ai = vscode.workspace.getConfiguration('adrGuard.ai', scope);
  const policy = analysis.get<string>('driftPolicy', 'advisory');
  const catalog = governance.get<string>('catalog', 'standard');
  const provider = ai.get<string>('provider', '');
  return {
    impactManifest: analysis.get<string>('impactManifest', '.adrguard-impact.json').trim(),
    driftRules: analysis.get<string>('driftRules', '.adrguard-fitness.json').trim(),
    driftBaseline: analysis.get<string>('driftBaseline', '').trim(),
    driftPolicy: policy === 'enforce' ? 'enforce' : 'advisory',
    catalog: catalog === 'enriched' ? 'enriched' : 'standard',
    provider: ['openai', 'anthropic', 'gemini', 'openai-compatible'].includes(provider) ? provider as FeatureConfiguration['provider'] : '',
    model: ai.get<string>('model', '').trim(),
    endpoint: ai.get<string>('endpoint', '').trim(),
  };
}

function clampInteger(value: number, minimum: number, maximum: number): number {
  if (!Number.isFinite(value)) {
    return minimum;
  }
  return Math.min(maximum, Math.max(minimum, Math.trunc(value)));
}
