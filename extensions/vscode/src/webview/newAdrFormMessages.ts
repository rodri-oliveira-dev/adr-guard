import path from 'node:path';
import { editFormMaximumInputCharacters, editFormSchemaVersion } from '../contracts/editForm';
import { newAdrFormMaximumInputCharacters, newAdrFormSchemaVersion } from '../contracts/newForm';
import { parseVisualSettingsValues, type VisualSettingsValues } from './settingsModel';

export interface FormValues {
  readonly workspace: string;
  readonly directory: string;
  readonly template: 'configured' | 'minimal' | 'extended';
  readonly culture: 'en-US' | 'pt-BR';
  readonly title: string;
  readonly fields: Readonly<Record<string, string>>;
}

export interface FormMessage {
  readonly type: 'ready' | 'describe' | 'preview' | 'create' | 'traditional' | 'cancel'
    | 'showList' | 'newAdr' | 'refreshList' | 'selectWorkspace' | 'openAdr' | 'revealCreated'
    | 'editAdr' | 'saveEdit' | 'initializeRepository' | 'checkInstallation'
    | 'openSettings' | 'cancelSettings' | 'saveSettings' | 'checkSettingsInstallation';
  readonly values?: FormValues;
  readonly editValues?: EditValues;
  readonly workspace?: string;
  readonly relativePath?: string;
  readonly state?: ListState;
  readonly settings?: VisualSettingsValues;
}

export interface EditValues {
  readonly workspace: string;
  readonly relativePath: string;
  readonly fingerprint: string;
  readonly title: string;
  readonly fields: Readonly<Record<string, string>>;
}

export interface ListState {
  readonly search: string;
  readonly status: string;
  readonly sort: 'number-desc' | 'number-asc' | 'title-asc' | 'date-desc';
  readonly scrollTop: number;
}

const maximumValueCharacters = 32 * 1024;

export function parseNewAdrFormMessage(value: unknown): FormMessage | undefined {
  if (visualFormInputLimit(value) !== undefined) return undefined;
  if (!isRecord(value) || typeof value.type !== 'string') return undefined;
  if (value.type === 'ready' || value.type === 'traditional') {
    return Object.keys(value).length === 1 ? { type: value.type } : undefined;
  }
  if (value.type === 'cancel') {
    if (Object.keys(value).length === 1) return { type: value.type };
    if (!isWorkspaceMessage(value, true) || (value.state !== undefined && !isListState(value.state))) return undefined;
    return { type: value.type, workspace: value.workspace,
      ...(value.state === undefined ? {} : { state: value.state }) };
  }
  if (value.type === 'revealCreated') {
    return isWorkspaceMessage(value) ? { type: value.type, workspace: value.workspace } : undefined;
  }
  if (value.type === 'saveSettings') {
    const settings = parseVisualSettingsValues(value.settings);
    if (settings === undefined || (value.state !== undefined && !isListState(value.state))
      || Object.keys(value).some((key) => !['type', 'settings', 'state'].includes(key))) return undefined;
    return { type: value.type, settings, ...(value.state === undefined ? {} : { state: value.state }) };
  }
  if (value.type === 'newAdr' || value.type === 'selectWorkspace' || value.type === 'showList'
    || value.type === 'openSettings' || value.type === 'cancelSettings'
    || value.type === 'checkSettingsInstallation'
    || value.type === 'refreshList' || value.type === 'initializeRepository' || value.type === 'checkInstallation') {
    if (!isWorkspaceMessage(value, true) || (value.state !== undefined && !isListState(value.state))) return undefined;
    return { type: value.type, workspace: value.workspace, ...(value.state === undefined ? {} : { state: value.state }) };
  }
  if (value.type === 'openAdr') {
    if (!isBoundedString(value.workspace, 4096) || !isBoundedString(value.relativePath, 4096)
      || !isSafeRelativeFile(value.relativePath)
      || Object.keys(value).some((key) => !['type', 'workspace', 'relativePath'].includes(key))) return undefined;
    return { type: value.type, workspace: value.workspace, relativePath: value.relativePath };
  }
  if (value.type === 'editAdr') {
    if (!isBoundedString(value.workspace, 4096) || !isBoundedString(value.relativePath, 4096)
      || !isSafeRelativeFile(value.relativePath) || (value.state !== undefined && !isListState(value.state))
      || Object.keys(value).some((key) => !['type', 'workspace', 'relativePath', 'state'].includes(key))) return undefined;
    return { type: value.type, workspace: value.workspace, relativePath: value.relativePath,
      ...(value.state === undefined ? {} : { state: value.state }) };
  }
  if (value.type === 'saveEdit') {
    if (!isEditValues(value.editValues)
      || Object.keys(value).some((key) => key !== 'type' && key !== 'editValues')) return undefined;
    return { type: value.type, editValues: value.editValues };
  }
  if (value.type !== 'describe' && value.type !== 'preview' && value.type !== 'create') return undefined;
  if (!isFormValues(value.values) || Object.keys(value).some((key) => key !== 'type' && key !== 'values')) return undefined;
  return { type: value.type, values: value.values };
}

export function visualFormInputLimit(value: unknown): 'creation' | 'edit' | undefined {
  if (!isRecord(value) || typeof value.type !== 'string') return undefined;
  try {
    if ((value.type === 'describe' || value.type === 'preview' || value.type === 'create') && isRecord(value.values)) {
      const input = JSON.stringify({
        schemaVersion: newAdrFormSchemaVersion,
        operation: value.type,
        ...(value.type === 'describe' ? {} : { title: value.values.title, fields: value.values.fields }),
      });
      return typeof input === 'string' && input.length > newAdrFormMaximumInputCharacters ? 'creation' : undefined;
    }
    if (value.type === 'saveEdit' && isRecord(value.editValues)) {
      const input = JSON.stringify({
        schemaVersion: editFormSchemaVersion,
        operation: 'save',
        fingerprint: value.editValues.fingerprint,
        title: value.editValues.title,
        fields: value.editValues.fields,
      });
      return typeof input === 'string' && input.length > editFormMaximumInputCharacters ? 'edit' : undefined;
    }
  } catch {
    return value.type === 'saveEdit' ? 'edit' : 'creation';
  }
  return undefined;
}

function isEditValues(value: unknown): value is EditValues {
  if (!isRecord(value) || Object.keys(value).some((key) => ![
    'workspace', 'relativePath', 'fingerprint', 'title', 'fields',
  ].includes(key))) return false;
  return isBoundedString(value.workspace, 4096)
    && isBoundedString(value.relativePath, 4096) && isSafeRelativeFile(value.relativePath)
    && typeof value.fingerprint === 'string' && /^[A-F0-9]{64}$/u.test(value.fingerprint)
    && isBoundedString(value.title, 512) && isRecord(value.fields)
    && Object.keys(value.fields).length <= 64
    && Object.entries(value.fields).every(([key, fieldValue]) =>
      /^(?:metadata|section)-[0-9]{1,2}$/u.test(key) && isBoundedString(fieldValue, maximumValueCharacters));
}

function isWorkspaceMessage(value: Record<string, unknown>, allowState = false): value is Record<string, unknown> & { workspace: string } {
  return isBoundedString(value.workspace, 4096)
    && Object.keys(value).every((key) => ['type', 'workspace', ...(allowState ? ['state'] : [])].includes(key));
}

function isListState(value: unknown): value is ListState {
  if (!isRecord(value) || Object.keys(value).some((key) => !['search', 'status', 'sort', 'scrollTop'].includes(key))) return false;
  return isBoundedString(value.search, 512) && isBoundedString(value.status, 200)
    && ['number-desc', 'number-asc', 'title-asc', 'date-desc'].includes(String(value.sort))
    && typeof value.scrollTop === 'number' && Number.isFinite(value.scrollTop)
    && value.scrollTop >= 0 && value.scrollTop <= 10_000_000;
}

function isSafeRelativeFile(value: string): boolean {
  return value.length > 0 && !path.isAbsolute(value) && !value.split(/[\\/]/u).includes('..')
    && path.extname(value).toLowerCase() === '.md';
}

export class VisualCreationSubmission {
  private active = false;

  public get isActive(): boolean { return this.active; }

  public accepts(): boolean {
    return !this.active;
  }

  public begin(): boolean {
    if (this.active) return false;
    this.active = true;
    return true;
  }

  public end(): void {
    this.active = false;
  }
}

function isFormValues(value: unknown): value is FormValues {
  if (!isRecord(value) || Object.keys(value).some((key) => ![
    'workspace', 'directory', 'template', 'culture', 'title', 'fields',
  ].includes(key))) return false;
  if (!isBoundedString(value.workspace, 4096)
    || !isBoundedString(value.directory, 4096)
    || !isSafeRelativeDirectory(value.directory)
    || (value.template !== 'configured' && value.template !== 'minimal' && value.template !== 'extended')
    || (value.culture !== 'en-US' && value.culture !== 'pt-BR')
    || !isBoundedString(value.title, 512)
    || !isRecord(value.fields)
    || Object.keys(value.fields).length > 10) return false;
  return Object.entries(value.fields).every(([key, fieldValue]) =>
    /^[a-z][A-Za-z]+$/u.test(key) && isBoundedString(fieldValue, maximumValueCharacters));
}

function isSafeRelativeDirectory(value: string): boolean {
  return value.trim().length > 0
    && !path.isAbsolute(value)
    && !value.split(/[\\/]/u).includes('..');
}

function isBoundedString(value: unknown, maximum: number): value is string {
  return typeof value === 'string' && value.length <= maximum && !value.includes('\0');
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
