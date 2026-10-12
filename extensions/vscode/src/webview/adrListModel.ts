import type { AdrCatalogEntry } from '../explorer/catalog';

export type AdrListSort = 'number-desc' | 'number-asc' | 'title-asc' | 'date-desc';

export interface AdrListQuery {
  readonly search: string;
  readonly status: string;
  readonly sort: AdrListSort;
}

export class LatestListRequest {
  private generation = 0;

  public begin(): number { return ++this.generation; }

  public isCurrent(request: number): boolean { return request === this.generation; }

  public invalidate(): void { this.generation += 1; }
}

export function listStatuses(entries: readonly AdrCatalogEntry[]): string[] {
  return [...new Set(entries.flatMap((entry) => entry.metadata.status === undefined ? [] : [entry.metadata.status]))]
    .sort((left, right) => left.localeCompare(right, undefined, { sensitivity: 'base' }));
}

export function filterAndSortAdrs(
  entries: readonly AdrCatalogEntry[],
  query: AdrListQuery,
): AdrCatalogEntry[] {
  const needle = query.search.trim().toLocaleLowerCase();
  const filtered = entries.filter((entry) => {
    const matchesStatus = query.status.length === 0 || entry.metadata.status === query.status;
    const haystack = [entry.metadata.title, entry.metadata.id, entry.relativePath]
      .filter((value): value is string => value !== undefined)
      .join('\0')
      .toLocaleLowerCase();
    return matchesStatus && (needle.length === 0 || haystack.includes(needle));
  });
  return filtered.sort((left, right) => compareEntries(left, right, query.sort));
}

function compareEntries(left: AdrCatalogEntry, right: AdrCatalogEntry, sort: AdrListSort): number {
  const pathOrder = left.relativePath.localeCompare(right.relativePath, 'en', { sensitivity: 'case' });
  if (sort === 'title-asc') {
    return compareOptional(left.metadata.title, right.metadata.title, false) || pathOrder;
  }
  if (sort === 'date-desc') {
    return compareOptional(left.metadata.date, right.metadata.date, true) || pathOrder;
  }
  return compareNumbers(left.metadata.id, right.metadata.id, sort === 'number-desc') || pathOrder;
}

function compareNumbers(left: string | undefined, right: string | undefined, descending: boolean): number {
  if (left === undefined) return right === undefined ? 0 : 1;
  if (right === undefined) return -1;
  const comparison = BigInt(left) < BigInt(right) ? -1 : BigInt(left) > BigInt(right) ? 1 : 0;
  return descending ? -comparison : comparison;
}

function compareOptional(left: string | undefined, right: string | undefined, descending: boolean): number {
  if (left === undefined) return right === undefined ? 0 : 1;
  if (right === undefined) return -1;
  const comparison = left.localeCompare(right, undefined, { sensitivity: 'base' });
  return descending ? -comparison : comparison;
}
