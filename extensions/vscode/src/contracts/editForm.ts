export const editFormSchemaVersion = '1.0';
export const editFormMaximumInputCharacters = 384 * 1024;

export interface EditFormField {
  readonly id: string;
  readonly label: string;
  readonly kind: 'status' | 'section' | 'metadata';
  readonly required: boolean;
  readonly value: string;
  readonly options?: readonly string[];
}

export interface EditFormLoadResponse {
  readonly schemaVersion: typeof editFormSchemaVersion;
  readonly operation: 'load';
  readonly format: 'canonical' | 'madr-4';
  readonly path: string;
  readonly fingerprint: string;
  readonly title: string;
  readonly fields: readonly EditFormField[];
  readonly written: boolean;
}

export interface EditFormSaveResponse {
  readonly schemaVersion: typeof editFormSchemaVersion;
  readonly operation: 'save';
  readonly fingerprint: string;
  readonly written: boolean;
}

export type EditFormResponse = EditFormLoadResponse | EditFormSaveResponse;

export function parseEditFormResponse(value: string): EditFormResponse {
  const parsed: unknown = JSON.parse(value);
  if (!isRecord(parsed) || parsed.schemaVersion !== editFormSchemaVersion
    || (parsed.operation !== 'load' && parsed.operation !== 'save')
    || !/^[A-F0-9]{64}$/u.test(String(parsed.fingerprint))
    || typeof parsed.written !== 'boolean') throw new Error('ADR Guard returned an invalid edit form response.');
  if (parsed.operation === 'save') {
    if (!hasOnly(parsed, ['schemaVersion', 'operation', 'fingerprint', 'written'])) {
      throw new Error('ADR Guard returned an invalid edit form response.');
    }
    return parsed as unknown as EditFormSaveResponse;
  }
  if (!hasOnly(parsed, [
    'schemaVersion', 'operation', 'format', 'path', 'fingerprint', 'title', 'fields', 'written',
  ]) || (parsed.format !== 'canonical' && parsed.format !== 'madr-4')
    || !boundedString(parsed.path, 4096)
    || !boundedString(parsed.title, 512) || !Array.isArray(parsed.fields) || parsed.fields.length > 64
    || parsed.written) throw new Error('ADR Guard returned an invalid edit form response.');
  const fields = parsed.fields.map(parseField);
  if (new Set(fields.map((field) => field.id)).size !== fields.length) throw new Error('ADR Guard returned an inconsistent edit form response.');
  return { ...parsed, fields } as unknown as EditFormLoadResponse;
}

function parseField(value: unknown): EditFormField {
  if (!isRecord(value) || !hasOnly(value, ['id', 'label', 'kind', 'required', 'value', 'options'])
    || !/^(?:metadata|section)-[0-9]{1,2}$/u.test(String(value.id))
    || !boundedString(value.label, 120)
    || (value.kind !== 'status' && value.kind !== 'section' && value.kind !== 'metadata')
    || typeof value.required !== 'boolean' || !boundedString(value.value, 32 * 1024)
    || (value.options !== undefined && (!Array.isArray(value.options) || value.options.length > 64
      || !value.options.every((option) => boundedString(option, 64))))) {
    throw new Error('ADR Guard returned an invalid edit field.');
  }
  return value as unknown as EditFormField;
}

function boundedString(value: unknown, maximum: number): value is string {
  return typeof value === 'string' && value.length <= maximum && !value.includes('\0');
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function hasOnly(value: Record<string, unknown>, keys: readonly string[]): boolean {
  return Object.keys(value).every((key) => keys.includes(key)) && keys.filter((key) => key !== 'options').every((key) => key in value);
}
