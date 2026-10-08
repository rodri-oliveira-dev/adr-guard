const diagnosticCode = /^ADR[0-9]{3}$/u;

export interface AdrCheckSummary {
  readonly files: number;
  readonly diagnostics: number;
}

export interface AdrCheckBaseline {
  readonly new: number;
  readonly existing: number;
  readonly resolved: number;
}

export interface AdrCheckDiagnostic {
  readonly code: string;
  readonly message: string;
  readonly file: string;
  readonly baselineState?: 'new' | 'existing';
}

export interface AdrCheckReport {
  readonly schemaVersion: '1.0';
  readonly valid: boolean;
  readonly summary: AdrCheckSummary;
  readonly files: readonly string[];
  readonly diagnostics: readonly AdrCheckDiagnostic[];
  readonly baseline?: AdrCheckBaseline;
}

export class CheckReportError extends Error {
  public constructor(message: string) {
    super(message);
    this.name = 'CheckReportError';
  }
}

export function parseCheckReport(output: string): AdrCheckReport {
  if (output.trim().length === 0) {
    throw new CheckReportError('stdout was empty');
  }
  let value: unknown;
  try {
    value = JSON.parse(output) as unknown;
  } catch {
    throw new CheckReportError('stdout was not valid JSON');
  }
  const root = objectWithKeys(value, ['schemaVersion', 'valid', 'summary', 'files', 'diagnostics'], ['baseline'], 'report');
  if (root.schemaVersion !== '1.0') {
    throw new CheckReportError('unsupported schemaVersion');
  }
  if (typeof root.valid !== 'boolean') {
    throw new CheckReportError('valid must be a boolean');
  }
  const summaryObject = objectWithKeys(root.summary, ['files', 'diagnostics'], [], 'summary');
  const summary = {
    files: nonNegativeInteger(summaryObject.files, 'summary.files'),
    diagnostics: nonNegativeInteger(summaryObject.diagnostics, 'summary.diagnostics'),
  };
  if (!Array.isArray(root.files) || !root.files.every(nonEmptyString)) {
    throw new CheckReportError('files must contain non-empty strings');
  }
  if (!Array.isArray(root.diagnostics)) {
    throw new CheckReportError('diagnostics must be an array');
  }
  const diagnostics = root.diagnostics.map((item, index) => parseDiagnostic(item, index));
  if (summary.files !== root.files.length || summary.diagnostics !== diagnostics.length) {
    throw new CheckReportError('summary counts do not match report arrays');
  }
  const baseline = root.baseline === undefined ? undefined : parseBaseline(root.baseline);
  const baselineStates = diagnostics.map((item) => item.baselineState);
  if (baseline === undefined && baselineStates.some((state) => state !== undefined)) {
    throw new CheckReportError('baselineState requires a baseline summary');
  }
  if (baseline !== undefined) {
    if (baselineStates.some((state) => state === undefined)
      || baseline.new !== baselineStates.filter((state) => state === 'new').length
      || baseline.existing !== baselineStates.filter((state) => state === 'existing').length
      || root.valid !== (baseline.new === 0)) {
      throw new CheckReportError('baseline summary, states, and valid must agree');
    }
  }
  return {
    schemaVersion: '1.0',
    valid: root.valid,
    summary,
    files: [...root.files] as string[],
    diagnostics,
    ...(baseline === undefined ? {} : { baseline }),
  };
}

function parseDiagnostic(value: unknown, index: number): AdrCheckDiagnostic {
  const item = objectWithKeys(value, ['code', 'message', 'file'], ['baselineState'], `diagnostics[${index}]`);
  if (typeof item.code !== 'string' || !diagnosticCode.test(item.code)) {
    throw new CheckReportError(`diagnostics[${index}].code is invalid`);
  }
  if (!nonEmptyString(item.message) || !nonEmptyString(item.file)) {
    throw new CheckReportError(`diagnostics[${index}] requires a message and file`);
  }
  if (item.baselineState !== undefined && item.baselineState !== 'new' && item.baselineState !== 'existing') {
    throw new CheckReportError(`diagnostics[${index}].baselineState is invalid`);
  }
  return {
    code: item.code,
    message: item.message,
    file: item.file,
    ...(item.baselineState === undefined ? {} : { baselineState: item.baselineState }),
  };
}

function parseBaseline(value: unknown): AdrCheckBaseline {
  const baseline = objectWithKeys(value, ['new', 'existing', 'resolved'], [], 'baseline');
  return {
    new: nonNegativeInteger(baseline.new, 'baseline.new'),
    existing: nonNegativeInteger(baseline.existing, 'baseline.existing'),
    resolved: nonNegativeInteger(baseline.resolved, 'baseline.resolved'),
  };
}

function objectWithKeys(
  value: unknown,
  required: readonly string[],
  optional: readonly string[],
  name: string,
): Record<string, unknown> {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    throw new CheckReportError(`${name} must be an object`);
  }
  const object = value as Record<string, unknown>;
  const allowed = new Set([...required, ...optional]);
  if (required.some((key) => !(key in object)) || Object.keys(object).some((key) => !allowed.has(key))) {
    throw new CheckReportError(`${name} has missing or unknown properties`);
  }
  return object;
}

function nonNegativeInteger(value: unknown, name: string): number {
  if (typeof value !== 'number' || !Number.isInteger(value) || value < 0) {
    throw new CheckReportError(`${name} must be a non-negative integer`);
  }
  return value;
}

function nonEmptyString(value: unknown): value is string {
  return typeof value === 'string' && value.length > 0;
}
