export const newAdrFormSchemaVersion = '1.0' as const;
export const newAdrFormMaximumInputCharacters = 128 * 1024;

export type NewAdrFormOperation = 'describe' | 'preview' | 'create';

export interface NewAdrFormField {
  readonly id: string;
  readonly required: boolean;
}

export interface NewAdrFormResponse {
  readonly schemaVersion: typeof newAdrFormSchemaVersion;
  readonly operation: NewAdrFormOperation;
  readonly template: {
    readonly name: 'minimal' | 'extended' | 'custom';
    readonly compatible: boolean;
    readonly reason?: string;
    readonly fields: readonly NewAdrFormField[];
  };
  readonly path?: string;
  readonly markdown?: string;
  readonly written: boolean;
}

const fieldIds = new Set([
  'context',
  'decisionDrivers',
  'optionsConsidered',
  'decision',
  'rationale',
  'consequences',
  'positiveConsequences',
  'negativeConsequences',
  'risks',
  'references',
]);

export function parseNewAdrFormResponse(value: string): NewAdrFormResponse {
  let parsed: unknown;
  try {
    parsed = JSON.parse(value) as unknown;
  } catch (error: unknown) {
    throw new Error('The CLI returned malformed visual ADR form JSON.', { cause: error });
  }
  if (!isRecord(parsed) || !hasOnlyKeys(parsed, [
    'schemaVersion', 'operation', 'template', 'path', 'markdown', 'written',
  ])) {
    throw new Error('The CLI returned an invalid visual ADR form contract.');
  }
  if (parsed.schemaVersion !== newAdrFormSchemaVersion
    || !isOperation(parsed.operation)
    || typeof parsed.written !== 'boolean'
    || !isOptionalString(parsed.path)
    || !isOptionalString(parsed.markdown)
    || !isTemplate(parsed.template)) {
    throw new Error('The CLI returned an incompatible visual ADR form contract.');
  }
  return parsed as unknown as NewAdrFormResponse;
}

function isTemplate(value: unknown): boolean {
  if (!isRecord(value) || !hasOnlyKeys(value, ['name', 'compatible', 'reason', 'fields'])) return false;
  if (value.name !== 'minimal' && value.name !== 'extended' && value.name !== 'custom') return false;
  if (typeof value.compatible !== 'boolean' || !isOptionalString(value.reason) || !Array.isArray(value.fields)) return false;
  const seen = new Set<string>();
  return value.fields.length <= fieldIds.size && value.fields.every((field) => {
    if (!isRecord(field) || !hasOnlyKeys(field, ['id', 'required'])) return false;
    if (typeof field.id !== 'string' || !fieldIds.has(field.id) || seen.has(field.id)) return false;
    seen.add(field.id);
    return typeof field.required === 'boolean';
  });
}

function isOperation(value: unknown): value is NewAdrFormOperation {
  return value === 'describe' || value === 'preview' || value === 'create';
}

function isOptionalString(value: unknown): boolean {
  return value === undefined || (typeof value === 'string' && value.length <= 131072 && !value.includes('\0'));
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function hasOnlyKeys(value: Record<string, unknown>, keys: readonly string[]): boolean {
  const allowed = new Set(keys);
  return Object.keys(value).every((key) => allowed.has(key));
}
