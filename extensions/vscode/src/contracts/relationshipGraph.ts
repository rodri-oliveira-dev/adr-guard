export const relationshipGraphSchemaVersion = '1.0' as const;
export const maximumRelationshipGraphReportNodes = 5_000;
export const maximumRelationshipGraphReportRelationships = 10_000;

export type RelationshipType = 'superseded-by' | 'supersedes' | 'depends-on';
export type RelationshipResolution = 'resolved' | 'unresolved' | 'ambiguous';

export interface RelationshipGraphNode {
  readonly id: string;
  readonly title: string | null;
  readonly status: string | null;
  readonly path: string;
  readonly format: 'canonical' | 'madr-4';
}

export interface RelationshipGraphTarget { readonly id: string; readonly path: string }
export interface RelationshipGraphRelationship {
  readonly sourceId: string;
  readonly sourcePath: string;
  readonly type: RelationshipType;
  readonly declaredTarget: string;
  readonly resolution: RelationshipResolution;
  readonly targets: readonly RelationshipGraphTarget[];
}
export interface RelationshipGraphDiagnostic {
  readonly code: string;
  readonly message: string;
  readonly path: string;
  readonly severity: 'warning' | 'error';
  readonly line?: number;
  readonly column?: number;
}
export interface RelationshipGraphReport {
  readonly schemaVersion: typeof relationshipGraphSchemaVersion;
  readonly adrDirectory: string;
  readonly summary: { readonly nodes: number; readonly relationships: number; readonly resolved: number; readonly unresolved: number; readonly ambiguous: number; readonly diagnostics: number };
  readonly nodes: readonly RelationshipGraphNode[];
  readonly relationships: readonly RelationshipGraphRelationship[];
  readonly diagnostics: readonly RelationshipGraphDiagnostic[];
}

export class RelationshipGraphContractError extends Error {
  public constructor(message: string) { super(message); this.name = 'RelationshipGraphContractError'; }
}

export class RelationshipGraphReportLimitError extends Error {
  public constructor(public readonly nodes: number, public readonly relationships: number) {
    super(`Relationship graph exceeds the bounded report limit (${maximumRelationshipGraphReportNodes} nodes and ${maximumRelationshipGraphReportRelationships} relationships).`);
    this.name = 'RelationshipGraphReportLimitError';
  }
}

export function parseRelationshipGraphReport(input: string): RelationshipGraphReport {
  let parsed: unknown;
  try { parsed = JSON.parse(input) as unknown; } catch { throw invalid('malformed JSON'); }
  const root = exactRecord(parsed, ['schemaVersion', 'adrDirectory', 'summary', 'nodes', 'relationships', 'diagnostics'], 'report');
  if (root.schemaVersion !== relationshipGraphSchemaVersion) throw invalid('unsupported schemaVersion');
  const adrDirectory = safePath(root.adrDirectory, 'adrDirectory');
  const summary = parseSummary(root.summary);
  const nodes = array(root.nodes, 'nodes').map(parseNode);
  const relationships = array(root.relationships, 'relationships').map(parseRelationship);
  const diagnostics = array(root.diagnostics, 'diagnostics').map(parseDiagnostic);
  if (nodes.length !== summary.nodes || relationships.length !== summary.relationships || diagnostics.length !== summary.diagnostics
    || relationships.filter((item) => item.resolution === 'resolved').length !== summary.resolved
    || relationships.filter((item) => item.resolution === 'unresolved').length !== summary.unresolved
    || relationships.filter((item) => item.resolution === 'ambiguous').length !== summary.ambiguous) throw invalid('inconsistent summary');
  if (nodes.length > maximumRelationshipGraphReportNodes || relationships.length > maximumRelationshipGraphReportRelationships) {
    throw new RelationshipGraphReportLimitError(nodes.length, relationships.length);
  }
  const nodeKeys = new Set(nodes.map((node) => `${node.id}\0${node.path}`));
  if (nodeKeys.size !== nodes.length) throw invalid('duplicate node');
  for (const relationship of relationships) {
    if (!nodeKeys.has(`${relationship.sourceId}\0${relationship.sourcePath}`)
      || relationship.targets.some((target) => !nodeKeys.has(`${target.id}\0${target.path}`))) throw invalid('relationship references an unknown node');
  }
  return { schemaVersion: relationshipGraphSchemaVersion, adrDirectory, summary, nodes, relationships, diagnostics };
}

function parseSummary(value: unknown): RelationshipGraphReport['summary'] {
  const record = exactRecord(value, ['nodes', 'relationships', 'resolved', 'unresolved', 'ambiguous', 'diagnostics'], 'summary');
  return { nodes: count(record.nodes, 'summary.nodes'), relationships: count(record.relationships, 'summary.relationships'), resolved: count(record.resolved, 'summary.resolved'), unresolved: count(record.unresolved, 'summary.unresolved'), ambiguous: count(record.ambiguous, 'summary.ambiguous'), diagnostics: count(record.diagnostics, 'summary.diagnostics') };
}

function parseNode(value: unknown): RelationshipGraphNode {
  const record = exactRecord(value, ['id', 'title', 'status', 'path', 'format'], 'node');
  const format = oneOf(record.format, ['canonical', 'madr-4'] as const, 'node.format');
  return { id: text(record.id, 'node.id'), title: nullableText(record.title, 'node.title'), status: nullableText(record.status, 'node.status'), path: safePath(record.path, 'node.path'), format };
}

function parseRelationship(value: unknown): RelationshipGraphRelationship {
  const record = exactRecord(value, ['sourceId', 'sourcePath', 'type', 'declaredTarget', 'resolution', 'targets'], 'relationship');
  const resolution = oneOf(record.resolution, ['resolved', 'unresolved', 'ambiguous'] as const, 'relationship.resolution');
  const targets = array(record.targets, 'relationship.targets').map((target) => {
    const item = exactRecord(target, ['id', 'path'], 'target');
    return { id: text(item.id, 'target.id'), path: safePath(item.path, 'target.path') };
  });
  if ((resolution === 'resolved' && targets.length !== 1) || (resolution === 'unresolved' && targets.length !== 0) || (resolution === 'ambiguous' && targets.length < 2)) throw invalid('targets do not match resolution');
  return { sourceId: text(record.sourceId, 'relationship.sourceId'), sourcePath: safePath(record.sourcePath, 'relationship.sourcePath'), type: oneOf(record.type, ['superseded-by', 'supersedes', 'depends-on'] as const, 'relationship.type'), declaredTarget: text(record.declaredTarget, 'relationship.declaredTarget'), resolution, targets };
}

function parseDiagnostic(value: unknown): RelationshipGraphDiagnostic {
  const record = exactRecord(value, ['code', 'message', 'path', 'severity'], 'diagnostic', ['line', 'column']);
  const code = text(record.code, 'diagnostic.code');
  if (!/^ADR[0-9]{3}$/u.test(code)) throw invalid('invalid diagnostic code');
  const line = optionalPositiveInteger(record.line, 'diagnostic.line');
  const column = optionalPositiveInteger(record.column, 'diagnostic.column');
  return { code, message: text(record.message, 'diagnostic.message'), path: safePath(record.path, 'diagnostic.path'), severity: oneOf(record.severity, ['warning', 'error'] as const, 'diagnostic.severity'), ...(line === undefined ? {} : { line }), ...(column === undefined ? {} : { column }) };
}

function exactRecord(value: unknown, required: readonly string[], label: string, optional: readonly string[] = []): Record<string, unknown> {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) throw invalid(`${label} is not an object`);
  const record = value as Record<string, unknown>;
  const keys = Object.keys(record);
  if (required.some((key) => !(key in record)) || keys.some((key) => !required.includes(key) && !optional.includes(key))) throw invalid(`${label} has an invalid shape`);
  return record;
}
function array(value: unknown, label: string): unknown[] { if (!Array.isArray(value)) throw invalid(`${label} is not an array`); return value; }
function text(value: unknown, label: string): string { if (typeof value !== 'string' || value.length === 0 || value.length > 32_768) throw invalid(`${label} is invalid`); return value; }
function nullableText(value: unknown, label: string): string | null { return value === null ? null : text(value, label); }
function count(value: unknown, label: string): number { if (!Number.isSafeInteger(value) || (value as number) < 0) throw invalid(`${label} is invalid`); return value as number; }
function optionalPositiveInteger(value: unknown, label: string): number | undefined { if (value === undefined) return undefined; const result = count(value, label); if (result < 1) throw invalid(`${label} is invalid`); return result; }
function oneOf<const T extends readonly string[]>(value: unknown, allowed: T, label: string): T[number] { if (typeof value !== 'string' || !allowed.includes(value)) throw invalid(`${label} is invalid`); return value; }
function safePath(value: unknown, label: string): string { const result = text(value, label); if (/^(?:[/\\]|[A-Za-z]:)/u.test(result) || result.split('/').includes('..') || result.includes('\\') || result.includes('\0')) throw invalid(`${label} is unsafe`); return result; }
function invalid(detail: string): RelationshipGraphContractError { return new RelationshipGraphContractError(`ADR relationship graph JSON is invalid or unsupported: ${detail}.`); }
