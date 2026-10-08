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
  let title: string | undefined;
  let section: string | undefined;
  let sectionStatus: string | undefined;
  let fence: '`' | '~' | undefined;
  const relationships: AdrRelationshipMetadata[] = [];

  for (const line of lines) {
    const trimmed = line.trimStart();
    const fenceMatch = /^(`{3,}|~{3,})/u.exec(trimmed);
    if (fenceMatch?.[1] !== undefined) {
      const marker = fenceMatch[1][0] as '`' | '~';
      fence = fence === undefined ? marker : fence === marker ? undefined : fence;
      continue;
    }
    if (fence !== undefined) continue;
    const heading = /^(#{1,6})\s+(.+?)\s*#*\s*$/u.exec(trimmed);
    if (heading?.[1] !== undefined && heading[2] !== undefined) {
      if (heading[1].length === 1 && title === undefined) title = neutralText(heading[2]);
      section = heading[1].length === 2 ? heading[2].trim().toLowerCase() : undefined;
      continue;
    }
    if (section === 'status' && sectionStatus === undefined && trimmed.trim().length > 0) {
      sectionStatus = neutralText(trimmed);
    }
    const kind = section === undefined ? undefined : relationshipHeadings.get(section);
    if (kind !== undefined) relationships.push(...parseMarkdownLinks(line, kind));
  }

  const status = format === 'madr-4'
    ? frontMatter.get('status') ?? sectionStatus
    : sectionStatus;
  const statusTarget = format === 'madr-4'
    ? /^superseded\s+by\s+ADR-([0-9]{4})$/iu.exec(status ?? '')?.[1]
    : undefined;
  if (statusTarget !== undefined) {
    relationships.push({ kind: 'superseded-by', label: `ADR-${statusTarget}`, targetId: statusTarget });
  }

  return {
    ...(id === undefined ? {} : { id }),
    ...(title === undefined ? {} : { title }),
    ...(status === undefined ? {} : { status }),
    relationships: deduplicateRelationships(relationships),
  };
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
