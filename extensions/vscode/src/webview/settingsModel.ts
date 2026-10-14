import path from 'node:path';

export type InterfaceLanguagePreference = 'auto' | 'en-US' | 'pt-BR';
export type DocumentLanguage = 'en-US' | 'pt-BR';
export type DefaultTemplate = 'configured' | 'minimal' | 'extended';
export type SettingsAdrFormat = 'canonical' | 'madr-4';

export interface VisualSettingsValues {
  readonly workspace: string;
  readonly uiLanguage: InterfaceLanguagePreference;
  readonly documentLanguage: DocumentLanguage;
  readonly directory: string;
  readonly adrFormat: SettingsAdrFormat;
  readonly defaultTemplate: DefaultTemplate;
}

export interface ConfigurationWrite {
  readonly section: 'adrGuard.ui' | 'adrGuard.creation' | 'adrGuard.validation';
  readonly key: 'language' | 'documentLanguage' | 'defaultTemplate' | 'directory' | 'adrFormat';
  readonly value: string;
  readonly target: 'global' | 'workspaceFolder';
}

export function parseVisualSettingsValues(value: unknown): VisualSettingsValues | undefined {
  if (!isRecord(value) || Object.keys(value).some((key) => ![
    'workspace', 'uiLanguage', 'documentLanguage', 'directory', 'adrFormat', 'defaultTemplate',
  ].includes(key))) return undefined;
  if (!boundedString(value.workspace, 4096)
    || !['auto', 'en-US', 'pt-BR'].includes(String(value.uiLanguage))
    || !['en-US', 'pt-BR'].includes(String(value.documentLanguage))
    || !isSafeRelativeDirectory(value.directory)
    || !['canonical', 'madr-4'].includes(String(value.adrFormat))
    || !['configured', 'minimal', 'extended'].includes(String(value.defaultTemplate))) return undefined;
  return value as unknown as VisualSettingsValues;
}

export function configurationWrites(values: VisualSettingsValues): readonly ConfigurationWrite[] {
  return [
    { section: 'adrGuard.ui', key: 'language', value: values.uiLanguage, target: 'global' },
    { section: 'adrGuard.creation', key: 'documentLanguage', value: values.documentLanguage, target: 'workspaceFolder' },
    { section: 'adrGuard.creation', key: 'defaultTemplate', value: values.defaultTemplate, target: 'workspaceFolder' },
    { section: 'adrGuard.validation', key: 'directory', value: values.directory.trim(), target: 'workspaceFolder' },
    { section: 'adrGuard.validation', key: 'adrFormat', value: values.adrFormat, target: 'workspaceFolder' },
  ];
}

export function effectiveInterfaceLanguage(
  preference: InterfaceLanguagePreference,
  editorLanguage: string,
): DocumentLanguage {
  if (preference !== 'auto') return preference;
  return editorLanguage.toLowerCase() === 'pt-br' ? 'pt-BR' : 'en-US';
}

export function isSafeRelativeDirectory(value: unknown): value is string {
  if (!boundedString(value, 4096) || value.trim().length === 0) return false;
  const candidate = value.trim();
  return !path.posix.isAbsolute(candidate)
    && !path.win32.isAbsolute(candidate)
    && !candidate.split(/[\\/]/u).includes('..');
}

function boundedString(value: unknown, maximum: number): value is string {
  return typeof value === 'string' && value.length <= maximum && !value.includes('\0');
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
