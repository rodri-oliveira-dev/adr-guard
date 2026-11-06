import type { RelationshipType } from '../contracts/relationshipGraph';

export type RelationshipGraphMessage =
  | { readonly type: 'ready' }
  | { readonly type: 'refresh' }
  | { readonly type: 'openAdr'; readonly path: string }
  | { readonly type: 'focus'; readonly path: string | null }
  | { readonly type: 'filter'; readonly status: string | null; readonly relationshipType: RelationshipType | null };

const maximumPathLength = 4_096;
const maximumStatusLength = 32_768;

export function parseRelationshipGraphMessage(value: unknown): RelationshipGraphMessage | undefined {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) return undefined;
  const record = value as Record<string, unknown>;
  if (record.type === 'ready' || record.type === 'refresh') {
    return hasExactKeys(record, ['type']) ? { type: record.type } : undefined;
  }
  if (record.type === 'openAdr') {
    return hasExactKeys(record, ['type', 'path']) && boundedText(record.path, maximumPathLength)
      ? { type: 'openAdr', path: record.path }
      : undefined;
  }
  if (record.type === 'focus') {
    return hasExactKeys(record, ['type', 'path']) && (record.path === null || boundedText(record.path, maximumPathLength))
      ? { type: 'focus', path: record.path }
      : undefined;
  }
  if (record.type !== 'filter'
    || !hasExactKeys(record, ['type', 'status', 'relationshipType'])
    || (record.status !== null && !boundedText(record.status, maximumStatusLength))
    || !isRelationshipType(record.relationshipType)) return undefined;
  return { type: 'filter', status: record.status, relationshipType: record.relationshipType };
}

function boundedText(value: unknown, maximumLength: number): value is string {
  return typeof value === 'string' && value.length > 0 && value.length <= maximumLength;
}

function hasExactKeys(record: Record<string, unknown>, expected: readonly string[]): boolean {
  const keys = Object.keys(record);
  return keys.length === expected.length && keys.every((key) => expected.includes(key));
}

function isRelationshipType(value: unknown): value is RelationshipType | null {
  return value === null || value === 'depends-on' || value === 'supersedes' || value === 'superseded-by';
}
