import path from 'node:path';
import type { AdrFormat } from '../commands/arguments';

export type AdrRelationshipKind = 'reference' | 'superseded-by' | 'supersedes' | 'depends-on';

export interface AdrRelationshipMetadata {
  readonly kind: AdrRelationshipKind;
  readonly label: string;
  readonly destination?: string;
  readonly targetId?: string;
}

export interface AdrDisplayMetadata {
  readonly id?: string;
  readonly title?: string;
  readonly status?: string;
  readonly date?: string;
  readonly warning?: string;
  readonly relationships: readonly AdrRelationshipMetadata[];
}

const relationshipHeadings = new Map<string, AdrRelationshipKind>([
  ['references', 'reference'],
  ['superseded by', 'superseded-by'],
  ['supersedes', 'supersedes'],
  ['depends on', 'depends-on'],
  ['dependencies', 'depends-on'],
]);

/**
 * Extracts display-only metadata from conventional Markdown constructs. It does
 * not validate ADR structure, statuses, relationship integrity, or governance.
 */
export function parseAdrDisplayMetadata(
  fileName: string,
  markdown: string,
  format: AdrFormat,
): AdrDisplayMetadata {
  const rawId = /^([0-9]+)-.+\.md$/iu.exec(fileName)?.[1];
  const id = rawId === undefined ? undefined : normalizeAdrId(rawId);
  const lines = markdown.split(/\r?\n/u);
  const frontMatter = parseFrontMatter(lines);
  const parsed = parseDocumentBody(lines);
  const relationships = [...parsed.relationships];

  const status = format === 'madr-4'
    ? frontMatter.get('status') ?? parsed.sectionStatus
    : parsed.sectionStatus;
  const date = frontMatter.get('date');
  const statusTarget = format === 'madr-4'
    ? /^superseded\s+by\s+ADR-([0-9]{4})$/iu.exec(status ?? '')?.[1]
    : undefined;
  if (statusTarget !== undefined) {
    relationships.push({ kind: 'superseded-by', label: `ADR-${statusTarget}`, targetId: statusTarget });
  }

  return {
    ...(id === undefined ? {} : { id }),
    ...(parsed.title === undefined ? {} : { title: parsed.title }),
    ...(status === undefined ? {} : { status }),
    ...(date === undefined ? {} : { date }),
    relationships: deduplicateRelationships(relationships),
  };
}

function parseDocumentBody(lines: readonly string[]): {
  readonly title?: string;
  readonly sectionStatus?: string;
  readonly relationships: readonly AdrRelationshipMetadata[];
} {
  let state: { title?: string; section?: string; sectionStatus?: string; fence?: '`' | '~' } = {};
  const relationships: AdrRelationshipMetadata[] = [];
  for (const line of lines) state = processBodyLine(line, state, relationships);
  return { ...(state.title === undefined ? {} : { title: state.title }),
    ...(state.sectionStatus === undefined ? {} : { sectionStatus: state.sectionStatus }), relationships };
}

function processBodyLine(
  line: string,
  state: { title?: string; section?: string; sectionStatus?: string; fence?: '`' | '~' },
  relationships: AdrRelationshipMetadata[],
): typeof state {
  const trimmed = line.trimStart();
  const fenceMatch = /^(`{3,}|~{3,})/u.exec(trimmed);
  if (fenceMatch?.[1] !== undefined) return processFence(state, fenceMatch[1][0] as '`' | '~');
  if (state.fence !== undefined) return state;
  const heading = /^(#{1,6})\s+(.+?)\s*#*\s*$/u.exec(trimmed);
  if (heading?.[1] !== undefined && heading[2] !== undefined) return processHeading(state, heading[1], heading[2]);
  return processSectionContent(line, trimmed, state, relationships);
}

function processFence(
  state: { title?: string; section?: string; sectionStatus?: string; fence?: '`' | '~' },
  marker: '`' | '~',
): typeof state {
  if (state.fence !== marker) return state.fence === undefined ? { ...state, fence: marker } : state;
  const withoutFence = { ...state };
  delete withoutFence.fence;
  return withoutFence;
}

function processHeading(
  state: { title?: string; section?: string; sectionStatus?: string; fence?: '`' | '~' },
  markers: string,
  heading: string,
): typeof state {
  const title = markers.length === 1 && state.title === undefined ? neutralText(heading) : state.title;
  const section = markers.length === 2 ? heading.trim().toLowerCase() : undefined;
  const other = { ...state };
  delete other.title;
  delete other.section;
  return { ...other, ...(title === undefined ? {} : { title }),
    ...(section === undefined ? {} : { section }) };
}

function processSectionContent(
  line: string,
  trimmed: string,
  state: { title?: string; section?: string; sectionStatus?: string; fence?: '`' | '~' },
  relationships: AdrRelationshipMetadata[],
): typeof state {
  const sectionStatus = state.section === 'status' && state.sectionStatus === undefined
    && trimmed.trim().length > 0 ? neutralText(trimmed) : state.sectionStatus;
  const kind = state.section === undefined ? undefined : relationshipHeadings.get(state.section);
  if (kind !== undefined) relationships.push(...parseMarkdownLinks(line, kind));
  return { ...state, ...(sectionStatus === undefined ? {} : { sectionStatus }) };
}

function parseFrontMatter(lines: readonly string[]): Map<string, string> {
  const metadata = new Map<string, string>();
  if (lines[0]?.trim() !== '---') return metadata;
  for (const line of lines.slice(1)) {
    if (line.trim() === '---') break;
    const match = /^([^:]+):\s*(.*?)\s*$/u.exec(line);
    if (match?.[1] === undefined || match[2] === undefined) continue;
    const value = match[2].replace(/^(?:"(.*)"|'(.*)')$/u, '$1$2');
    const normalized = neutralText(value);
    if (normalized !== undefined) metadata.set(match[1].trim().toLowerCase(), normalized);
  }
  return metadata;
}

function parseMarkdownLinks(line: string, kind: AdrRelationshipKind): AdrRelationshipMetadata[] {
  const links: AdrRelationshipMetadata[] = [];
  for (const match of line.matchAll(/\[([^\]\r\n]+)\]\(([^\s)]+)(?:\s+["'][^"']*["'])?\)/gu)) {
    const label = match[1];
    const destination = match[2];
    const normalized = label === undefined ? undefined : neutralText(label);
    if (normalized !== undefined && destination !== undefined) {
      links.push({ kind, label: normalized, destination });
    }
  }
  return links;
}

function neutralText(value: string): string | undefined {
  const normalized = value.replace(/[*_`]/gu, '').trim();
  return normalized.length > 0 && normalized.length <= 200 ? normalized : undefined;
}

function deduplicateRelationships(
  relationships: readonly AdrRelationshipMetadata[],
): AdrRelationshipMetadata[] {
  const seen = new Set<string>();
  return relationships.filter((relationship) => {
    const key = `${relationship.kind}\0${relationship.destination ?? relationship.targetId ?? ''}`;
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}

export function displayId(fileName: string, id: string | undefined): string {
  return id === undefined ? path.basename(fileName, path.extname(fileName)) : id.padStart(4, '0');
}

function normalizeAdrId(value: string): string {
  return value.replace(/^0+(?=[0-9])/u, '').padStart(4, '0');
}
