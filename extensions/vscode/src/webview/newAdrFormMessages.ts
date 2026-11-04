import path from 'node:path';
import { editFormMaximumInputCharacters, editFormSchemaVersion } from '../contracts/editForm';
import { newAdrFormMaximumInputCharacters, newAdrFormSchemaVersion } from '../contracts/newForm';
import { parseVisualSettingsValues, type VisualSettingsValues } from './settingsModel';
import { isSafeMcpServerPath } from '../mcp/discovery';

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
    | 'openSettings' | 'cancelSettings' | 'saveSettings' | 'checkSettingsInstallation'
    | 'checkMcpConfiguration' | 'openMcpDocumentation';
  readonly values?: FormValues;
  readonly editValues?: EditValues;
  readonly workspace?: string;
  readonly relativePath?: string;
  readonly state?: ListState;
  readonly settings?: VisualSettingsValues;
  readonly mcpEnabled?: boolean;
  readonly mcpPath?: string;
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
  switch (value.type) {
    case 'ready': case 'traditional': return parseTypeOnlyMessage(value);
    case 'cancel': return parseCancelMessage(value);
    case 'revealCreated': return parseWorkspaceOnlyMessage(value);
    case 'saveSettings': return parseSaveSettingsMessage(value);
    case 'checkMcpConfiguration': return parseMcpConfigurationMessage(value);
    case 'openAdr': return parseAdrPathMessage(value, false);
    case 'editAdr': return parseAdrPathMessage(value, true);
    case 'saveEdit': return parseSaveEditMessage(value);
    case 'describe': case 'preview': case 'create': return parseCreationMessage(value);
    default: return workspaceStateMessageTypes.some((type) => type === value.type)
      ? parseWorkspaceStateMessage(value)
      : undefined;
  }
}

export function visualFormInputLimit(value: unknown): 'creation' | 'edit' | undefined {
  if (!isRecord(value) || typeof value.type !== 'string') return undefined;
  try {
    if (isCreationInput(value)) return exceedsCreationLimit(value) ? 'creation' : undefined;
    if (value.type === 'saveEdit' && isRecord(value.editValues)) {
      return exceedsEditLimit(value.editValues) ? 'edit' : undefined;
    }
  } catch {
    return value.type === 'saveEdit' ? 'edit' : 'creation';
  }
  return undefined;
}

const workspaceStateMessageTypes = [
  'newAdr', 'selectWorkspace', 'showList', 'openSettings', 'cancelSettings',
  'checkSettingsInstallation', 'openMcpDocumentation', 'refreshList',
  'initializeRepository', 'checkInstallation',
] as const;

function parseTypeOnlyMessage(value: Record<string, unknown>): FormMessage | undefined {
  return Object.keys(value).length === 1
    ? { type: value.type as 'ready' | 'traditional' }
    : undefined;
}

function parseCancelMessage(value: Record<string, unknown>): FormMessage | undefined {
  if (Object.keys(value).length === 1) return { type: 'cancel' };
  if (!isWorkspaceMessage(value, true) || !optionalListState(value.state)) return undefined;
  const state = value.state as ListState | undefined;
  return { type: 'cancel', workspace: value.workspace,
    ...(state === undefined ? {} : { state }) };
}

function parseWorkspaceOnlyMessage(value: Record<string, unknown>): FormMessage | undefined {
  return isWorkspaceMessage(value) ? { type: 'revealCreated', workspace: value.workspace } : undefined;
}

function parseWorkspaceStateMessage(value: Record<string, unknown>): FormMessage | undefined {
  if (!isWorkspaceMessage(value, true) || !optionalListState(value.state)) return undefined;
  const state = value.state as ListState | undefined;
  return { type: value.type as typeof workspaceStateMessageTypes[number], workspace: value.workspace,
    ...(state === undefined ? {} : { state }) };
}

function parseSaveSettingsMessage(value: Record<string, unknown>): FormMessage | undefined {
  const settings = parseVisualSettingsValues(value.settings);
  if (settings === undefined || !optionalListState(value.state)
    || !hasOnlyKeys(value, ['type', 'settings', 'state'])) return undefined;
  const state = value.state as ListState | undefined;
  return { type: 'saveSettings', settings,
    ...(state === undefined ? {} : { state }) };
}

function parseMcpConfigurationMessage(value: Record<string, unknown>): FormMessage | undefined {
  if (!isBoundedString(value.workspace, 4096) || typeof value.mcpEnabled !== 'boolean'
    || !isBoundedString(value.mcpPath, 4096)
    || (value.mcpPath.trim().length > 0 && !isSafeMcpServerPath(value.mcpPath.trim()))
    || !hasOnlyKeys(value, ['type', 'workspace', 'mcpEnabled', 'mcpPath'])) return undefined;
  return { type: 'checkMcpConfiguration', workspace: value.workspace,
    mcpEnabled: value.mcpEnabled, mcpPath: value.mcpPath };
}

function parseAdrPathMessage(value: Record<string, unknown>, allowState: boolean): FormMessage | undefined {
  if (!isBoundedString(value.workspace, 4096) || !isBoundedString(value.relativePath, 4096)
    || !isSafeRelativeFile(value.relativePath) || (allowState && !optionalListState(value.state))
    || !hasOnlyKeys(value, ['type', 'workspace', 'relativePath', ...(allowState ? ['state'] : [])])) return undefined;
  return { type: value.type as 'openAdr' | 'editAdr', workspace: value.workspace,
    relativePath: value.relativePath, ...(value.state === undefined ? {} : { state: value.state as ListState }) };
}

function parseSaveEditMessage(value: Record<string, unknown>): FormMessage | undefined {
  return isEditValues(value.editValues) && hasOnlyKeys(value, ['type', 'editValues'])
    ? { type: 'saveEdit', editValues: value.editValues }
    : undefined;
}

function parseCreationMessage(value: Record<string, unknown>): FormMessage | undefined {
  return isFormValues(value.values) && hasOnlyKeys(value, ['type', 'values'])
    ? { type: value.type as 'describe' | 'preview' | 'create', values: value.values }
    : undefined;
}

function optionalListState(value: unknown): boolean {
  return value === undefined || isListState(value);
}

function hasOnlyKeys(value: Record<string, unknown>, keys: readonly string[]): boolean {
  return Object.keys(value).every((key) => keys.includes(key));
}

function isCreationInput(value: Record<string, unknown>): boolean {
  return (value.type === 'describe' || value.type === 'preview' || value.type === 'create')
    && isRecord(value.values);
}

function exceedsCreationLimit(value: Record<string, unknown>): boolean {
  const values = value.values as Record<string, unknown>;
  const input = JSON.stringify({ schemaVersion: newAdrFormSchemaVersion, operation: value.type,
    ...(value.type === 'describe' ? {} : { title: values.title, fields: values.fields }) });
  return typeof input === 'string' && input.length > newAdrFormMaximumInputCharacters;
}

function exceedsEditLimit(editValues: Record<string, unknown>): boolean {
  const input = JSON.stringify({ schemaVersion: editFormSchemaVersion, operation: 'save',
    fingerprint: editValues.fingerprint, title: editValues.title, fields: editValues.fields });
  return typeof input === 'string' && input.length > editFormMaximumInputCharacters;
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
