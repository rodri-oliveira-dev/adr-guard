import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import type { AdrCatalogEntry } from '../../src/explorer/catalog';
import { filterAndSortAdrs, LatestListRequest, listStatuses } from '../../src/webview/adrListModel';

describe('ADR list model', () => {
  const entries = [
    entry('0010-cache.md', '0010', 'Use Cache', 'Accepted', '2026-10-10'),
    entry('0002-api.md', '0002', 'Public API', 'Proposed', '2026-10-11'),
    entry('notes.md', undefined, undefined, undefined, undefined),
  ];

  it('combines case-insensitive search and status filters', () => {
    assert.deepEqual(filterAndSortAdrs(entries, { search: 'CACHE', status: 'Accepted', sort: 'number-desc' })
      .map((item) => item.relativePath), ['0010-cache.md']);
    assert.deepEqual(filterAndSortAdrs(entries, { search: 'notes', status: '', sort: 'number-desc' })
      .map((item) => item.relativePath), ['notes.md']);
    assert.deepEqual(listStatuses(entries), ['Accepted', 'Proposed']);
  });

  it('sorts numbered ADRs deterministically and leaves unnumbered documents last', () => {
    assert.deepEqual(filterAndSortAdrs(entries, { search: '', status: '', sort: 'number-desc' })
      .map((item) => item.relativePath), ['0010-cache.md', '0002-api.md', 'notes.md']);
    assert.deepEqual(filterAndSortAdrs(entries, { search: '', status: '', sort: 'number-asc' })
      .map((item) => item.relativePath), ['0002-api.md', '0010-cache.md', 'notes.md']);
    assert.deepEqual(filterAndSortAdrs(entries, { search: '', status: '', sort: 'title-asc' })
      .map((item) => item.relativePath), ['0002-api.md', '0010-cache.md', 'notes.md']);
    assert.deepEqual(filterAndSortAdrs(entries, { search: '', status: '', sort: 'date-desc' })
      .map((item) => item.relativePath), ['0002-api.md', '0010-cache.md', 'notes.md']);
  });

  it('does not mutate the catalog while filtering and sorting', () => {
    const originalOrder = entries.map((item) => item.relativePath);
    filterAndSortAdrs(entries, { search: '', status: '', sort: 'title-asc' });
    assert.deepEqual(entries.map((item) => item.relativePath), originalOrder);
  });

  it('discards obsolete list responses', () => {
    const requests = new LatestListRequest();
    const first = requests.begin();
    const second = requests.begin();
    assert.equal(requests.isCurrent(first), false);
    assert.equal(requests.isCurrent(second), true);
    requests.invalidate();
    assert.equal(requests.isCurrent(second), false);
  });
});

function entry(relativePath: string, id: string | undefined, title: string | undefined, status: string | undefined, date: string | undefined): AdrCatalogEntry {
  return {
    filePath: `/workspace/docs/adr/${relativePath}`,
    relativePath,
    fileName: relativePath,
    metadata: { ...(id === undefined ? {} : { id }), ...(title === undefined ? {} : { title }), ...(status === undefined ? {} : { status }), ...(date === undefined ? {} : { date }), relationships: [] },
    relationships: [],
  };
}
