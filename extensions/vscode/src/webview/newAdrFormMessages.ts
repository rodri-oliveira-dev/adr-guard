import path from 'node:path';

export interface FormValues {
  readonly workspace: string;
  readonly directory: string;
  readonly template: 'configured' | 'minimal' | 'extended';
  readonly culture: 'en-US' | 'pt-BR';
  readonly title: string;
  readonly fields: Readonly<Record<string, string>>;
}

export interface FormMessage {
  readonly type: 'ready' | 'describe' | 'preview' | 'create' | 'traditional' | 'cancel';
  readonly values?: FormValues;
}

const maximumValueCharacters = 32 * 1024;

export function parseNewAdrFormMessage(value: unknown): FormMessage | undefined {
  if (!isRecord(value) || typeof value.type !== 'string') return undefined;
  if (value.type === 'ready' || value.type === 'traditional' || value.type === 'cancel') {
    return Object.keys(value).length === 1 ? { type: value.type } : undefined;
  }
  if (value.type !== 'describe' && value.type !== 'preview' && value.type !== 'create') return undefined;
  if (!isFormValues(value.values) || Object.keys(value).some((key) => key !== 'type' && key !== 'values')) return undefined;
  return { type: value.type, values: value.values };
}

export class VisualCreationSubmission {
  private active = false;

  public accepts(type: FormMessage['type']): boolean {
    return !this.active || type === 'cancel';
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
