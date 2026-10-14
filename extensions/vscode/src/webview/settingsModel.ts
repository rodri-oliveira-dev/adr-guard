import path from 'node:path';
import { isSafeConfiguredExecutablePath } from '../cli/discovery';
import { isSafeGitBaseReference } from '../commands/arguments';

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
  readonly onSave: boolean;
  readonly debounceMilliseconds: number;
  readonly baseline: string;
  readonly baseReference: string;
  readonly cliPath: string;
  readonly timeoutMilliseconds: number;
  readonly maxOutputBytes: number;
}

export interface ConfigurationWrite {
  readonly section: 'adrGuard.ui' | 'adrGuard.creation' | 'adrGuard.validation' | 'adrGuard.cli';
  readonly key: 'language' | 'documentLanguage' | 'defaultTemplate' | 'directory' | 'adrFormat'
    | 'onSave' | 'debounceMilliseconds' | 'baseline' | 'baseReference'
    | 'path' | 'timeoutMilliseconds' | 'maxOutputBytes';
  readonly value: string | number | boolean;
  readonly target: 'global' | 'workspaceFolder';
}

export async function applyConfigurationWrites(
  writes: readonly ConfigurationWrite[],
  currentValue: (write: ConfigurationWrite) => unknown,
  update: (write: ConfigurationWrite, value: unknown) => PromiseLike<void>,
): Promise<void> {
  const snapshots = writes.map((write) => ({ write, value: currentValue(write) }));
  let applied = 0;
  try {
    for (const write of writes) {
      await update(write, write.value);
      applied += 1;
    }
  } catch (error: unknown) {
    const rollbackFailures: unknown[] = [];
    for (let index = applied - 1; index >= 0; index -= 1) {
      const snapshot = snapshots[index];
      if (snapshot === undefined) continue;
      try {
        await update(snapshot.write, snapshot.value);
      } catch (rollbackError: unknown) {
        rollbackFailures.push(rollbackError);
      }
    }
    if (rollbackFailures.length > 0) {
      throw new AggregateError(
        [error, ...rollbackFailures],
        'Settings persistence and rollback failed.',
        { cause: error },
      );
    }
    throw error;
  }
}

export function parseVisualSettingsValues(value: unknown): VisualSettingsValues | undefined {
  if (!isRecord(value) || Object.keys(value).some((key) => ![
    'workspace', 'uiLanguage', 'documentLanguage', 'directory', 'adrFormat', 'defaultTemplate',
    'onSave', 'debounceMilliseconds', 'baseline', 'baseReference', 'cliPath',
    'timeoutMilliseconds', 'maxOutputBytes',
  ].includes(key))) return undefined;
  if (!boundedString(value.workspace, 4096)
    || !['auto', 'en-US', 'pt-BR'].includes(String(value.uiLanguage))
    || !['en-US', 'pt-BR'].includes(String(value.documentLanguage))
    || !isSafeRelativeDirectory(value.directory)
    || !['canonical', 'madr-4'].includes(String(value.adrFormat))
    || !['configured', 'minimal', 'extended'].includes(String(value.defaultTemplate))
    || typeof value.onSave !== 'boolean'
    || !integerInRange(value.debounceMilliseconds, 200, 5000)
    || !isSafeRelativeJsonFile(value.baseline)
    || !boundedString(value.baseReference, 256) || !isSafeGitBaseReference(value.baseReference.trim())
    || !boundedString(value.cliPath, 4096)
    || (value.cliPath.trim().length > 0 && !isSafeConfiguredExecutablePath(value.cliPath.trim()))
    || !integerInRange(value.timeoutMilliseconds, 1000, 120000)
    || !integerInRange(value.maxOutputBytes, 1024, 1048576)) return undefined;
  return value as unknown as VisualSettingsValues;
}

export function configurationWrites(values: VisualSettingsValues): readonly ConfigurationWrite[] {
  return [
    { section: 'adrGuard.ui', key: 'language', value: values.uiLanguage, target: 'global' },
    { section: 'adrGuard.creation', key: 'documentLanguage', value: values.documentLanguage, target: 'workspaceFolder' },
    { section: 'adrGuard.creation', key: 'defaultTemplate', value: values.defaultTemplate, target: 'workspaceFolder' },
    { section: 'adrGuard.validation', key: 'directory', value: values.directory.trim(), target: 'workspaceFolder' },
    { section: 'adrGuard.validation', key: 'adrFormat', value: values.adrFormat, target: 'workspaceFolder' },
    { section: 'adrGuard.validation', key: 'onSave', value: values.onSave, target: 'workspaceFolder' },
    { section: 'adrGuard.validation', key: 'debounceMilliseconds', value: values.debounceMilliseconds, target: 'workspaceFolder' },
    { section: 'adrGuard.validation', key: 'baseline', value: values.baseline.trim(), target: 'workspaceFolder' },
    { section: 'adrGuard.validation', key: 'baseReference', value: values.baseReference.trim(), target: 'workspaceFolder' },
    { section: 'adrGuard.cli', key: 'path', value: values.cliPath.trim(), target: 'global' },
    { section: 'adrGuard.cli', key: 'timeoutMilliseconds', value: values.timeoutMilliseconds, target: 'global' },
    { section: 'adrGuard.cli', key: 'maxOutputBytes', value: values.maxOutputBytes, target: 'global' },
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

export function isSafeRelativeJsonFile(value: unknown): value is string {
  return isSafeRelativeDirectory(value) && path.extname(value.trim()).toLowerCase() === '.json';
}

function integerInRange(value: unknown, minimum: number, maximum: number): value is number {
  return typeof value === 'number' && Number.isInteger(value) && value >= minimum && value <= maximum;
}

function boundedString(value: unknown, maximum: number): value is string {
  return typeof value === 'string' && value.length <= maximum && !value.includes('\0');
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
