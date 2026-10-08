import { open, lstat, readdir, realpath } from 'node:fs/promises';
import path from 'node:path';
import type { AdrFormat } from '../commands/arguments';
import { isResolvedPathWithin } from '../workspacePolicy';
import {
  parseAdrDisplayMetadata,
  type AdrDisplayMetadata,
  type AdrRelationshipKind,
} from './metadata';

const maximumMetadataBytes = 256 * 1024;

export interface AdrCatalogRelationship {
  readonly kind: AdrRelationshipKind;
  readonly label: string;
  readonly targetPath: string;
}

export interface AdrCatalogEntry {
  readonly filePath: string;
  readonly relativePath: string;
  readonly fileName: string;
  readonly metadata: AdrDisplayMetadata;
  readonly relationships: readonly AdrCatalogRelationship[];
}

export async function loadAdrCatalog(
  workspaceRoot: string,
  adrDirectory: string,
  format: AdrFormat,
): Promise<readonly AdrCatalogEntry[]> {
  const [realWorkspace, realDirectory] = await Promise.all([realpath(workspaceRoot), realpath(adrDirectory)]);
  if (!isResolvedPathWithin(realDirectory, realWorkspace)) {
    throw new Error('The ADR directory resolves outside the workspace.');
  }
  const files = await discoverMarkdownFiles(realDirectory);
  const preliminary = await mapWithConcurrency(files, 16, async (filePath) => ({
    filePath,
    relativePath: path.relative(realDirectory, filePath).replaceAll(path.sep, '/'),
    fileName: path.basename(filePath),
    metadata: parseAdrDisplayMetadata(path.basename(filePath), await readPrefix(filePath), format),
  }));
  const byId = new Map<string, typeof preliminary>();
  for (const entry of preliminary) {
    if (entry.metadata.id === undefined) continue;
    const existing = byId.get(entry.metadata.id) ?? [];
    existing.push(entry);
    byId.set(entry.metadata.id, existing);
  }
  return (await mapWithConcurrency(preliminary, 16, async (entry): Promise<AdrCatalogEntry> => ({
    ...entry,
    relationships: await resolveRelationships(entry, preliminary, byId, realDirectory),
  }))).sort((left, right) => left.relativePath.localeCompare(right.relativePath, 'en', { sensitivity: 'case' }));
}

async function discoverMarkdownFiles(directory: string): Promise<string[]> {
  const found: string[] = [];
  const pending = [directory];
  while (pending.length > 0) {
    const current = pending.pop();
    if (current === undefined) continue;
    const entries = await readdir(current, { withFileTypes: true });
    for (const entry of entries) {
      const candidate = path.join(current, entry.name);
      if (entry.isSymbolicLink()) continue;
      if (entry.isDirectory()) pending.push(candidate);
      else if (entry.isFile() && path.extname(entry.name).toLowerCase() === '.md'
        && entry.name.toLowerCase() !== 'readme.md') found.push(await realpath(candidate));
    }
  }
  return found.sort((left, right) => left.localeCompare(right, 'en', { sensitivity: 'case' }));
}

async function readPrefix(filePath: string): Promise<string> {
  const fileStat = await lstat(filePath);
  if (!fileStat.isFile() || fileStat.isSymbolicLink()) throw new Error('ADR metadata source is not a regular file.');
  const length = Math.min(fileStat.size, maximumMetadataBytes);
  const handle = await open(filePath, 'r');
  try {
    const buffer = Buffer.alloc(length);
    const { bytesRead } = await handle.read(buffer, 0, length, 0);
    return buffer.subarray(0, bytesRead).toString('utf8');
  } finally {
    await handle.close();
  }
}

async function resolveRelationships(
  source: { readonly filePath: string; readonly metadata: AdrDisplayMetadata },
  entries: readonly { readonly filePath: string }[],
  byId: ReadonlyMap<string, readonly { readonly filePath: string }[]>,
  adrDirectory: string,
): Promise<AdrCatalogRelationship[]> {
  const known = new Set(entries.map((entry) => normalizePath(entry.filePath)));
  const resolved: AdrCatalogRelationship[] = [];
  for (const relationship of source.metadata.relationships) {
    let targetPath: string | undefined;
    if (relationship.targetId !== undefined) {
      const matches = byId.get(relationship.targetId);
      if (matches?.length === 1) targetPath = matches[0]?.filePath;
    } else if (relationship.destination !== undefined) {
      targetPath = await resolveMarkdownDestination(source.filePath, relationship.destination, adrDirectory);
    }
    if (targetPath !== undefined && known.has(normalizePath(targetPath))) {
      resolved.push({ kind: relationship.kind, label: relationship.label, targetPath });
    }
  }
  return resolved;
}

export async function resolveMarkdownDestination(
  sourcePath: string,
  destination: string,
  adrDirectory: string,
): Promise<string | undefined> {
  if (destination.startsWith('#') || destination.startsWith('//') || destination.includes('\0')
    || destination.includes('\\') || /^[A-Za-z][A-Za-z0-9+.-]*:/u.test(destination)) return undefined;
  const pathPart = destination.split(/[?#]/u, 1)[0];
  if (pathPart === undefined || pathPart.length === 0) return undefined;
  let decoded: string;
  try {
    decoded = decodeURIComponent(pathPart);
  } catch {
    return undefined;
  }
  if (path.isAbsolute(decoded)) return undefined;
  try {
    const resolved = await realpath(path.resolve(path.dirname(sourcePath), decoded));
    return isResolvedPathWithin(resolved, adrDirectory) && path.extname(resolved).toLowerCase() === '.md'
      ? resolved
      : undefined;
  } catch {
    return undefined;
  }
}

function normalizePath(value: string): string {
  const resolved = path.resolve(value);
  return process.platform === 'win32' ? resolved.toLowerCase() : resolved;
}

async function mapWithConcurrency<T, R>(
  values: readonly T[],
  limit: number,
  transform: (value: T) => Promise<R>,
): Promise<R[]> {
  const results = new Array<R>(values.length);
  let next = 0;
  const worker = async (): Promise<void> => {
    while (next < values.length) {
      const index = next;
      next += 1;
      const value = values[index];
      if (value !== undefined) results[index] = await transform(value);
    }
  };
  await Promise.all(Array.from({ length: Math.min(limit, values.length) }, worker));
  return results;
}
