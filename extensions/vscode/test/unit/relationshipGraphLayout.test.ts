import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import type { RelationshipGraphReport } from '../../src/contracts/relationshipGraph';
import { layoutRelationshipGraph, maximumVisibleRelationshipGraphNodes, relationshipGraphStatuses } from '../../src/webview/relationshipGraphModel';

describe('relationship graph layout', () => {
  it('produces deterministic layers independent of input order', () => {
    const forward = report(false);
    const reversed = report(true);
    const first = layoutRelationshipGraph(forward);
    const second = layoutRelationshipGraph(reversed);
    assert.deepEqual(first, second);
    assert.deepEqual(first.nodes.map(({ id, x, y }) => ({ id, x, y })), [
      { id: 'ADR-1', x: 40, y: 40 }, { id: 'ADR-2', x: 360, y: 40 }, { id: 'ADR-3', x: 680, y: 40 },
    ]);
  });

  it('uses ordinal ordering when identifiers differ only by accents', () => {
    const nodes: RelationshipGraphReport['nodes'] = [
      { id: 'ADR-café', title: null, status: null, path: 'café.md', format: 'canonical' },
      { id: 'ADR-cafe', title: null, status: null, path: 'cafe.md', format: 'canonical' },
    ];
    const value: RelationshipGraphReport = { schemaVersion: '1.0', adrDirectory: '.', summary: { nodes: 2, relationships: 0, resolved: 0, unresolved: 0, ambiguous: 0, diagnostics: 0 }, nodes, relationships: [], diagnostics: [] };
    const forward = layoutRelationshipGraph(value);
    const reversed = layoutRelationshipGraph({ ...value, nodes: [...nodes].reverse() });
    assert.deepEqual(forward, reversed);
    assert.deepEqual(forward.nodes.map((node) => node.id), ['ADR-cafe', 'ADR-café']);
  });

  it('keeps cycles bounded and represents unresolved relationships as dangling edges', () => {
    const value = report(false);
    const cycle: RelationshipGraphReport = {
      ...value,
      summary: { nodes: 3, relationships: 4, resolved: 3, unresolved: 1, ambiguous: 0, diagnostics: 0 },
      relationships: [...value.relationships,
        { sourceId: 'ADR-3', sourcePath: 'docs/adr/3.md', type: 'depends-on', declaredTarget: 'ADR-1', resolution: 'resolved', targets: [{ id: 'ADR-1', path: 'docs/adr/1.md' }] },
        { sourceId: 'ADR-3', sourcePath: 'docs/adr/3.md', type: 'supersedes', declaredTarget: 'missing', resolution: 'unresolved', targets: [] }],
    };
    const layout = layoutRelationshipGraph(cycle);
    assert.ok(layout.width > 0 && layout.height > 0);
    assert.equal(layout.edges.at(-1)?.target, null);
    const unresolved = layout.edges.at(-1);
    if (unresolved === undefined) assert.fail('Expected an unresolved edge.');
    assert.equal(unresolved.resolution, 'unresolved');
    assert.ok(layout.width >= unresolved.source.x + 87);
  });

  it('lays out empty and disconnected graphs without invalid dimensions', () => {
    const empty: RelationshipGraphReport = { schemaVersion: '1.0', adrDirectory: 'docs/adr', summary: { nodes: 0, relationships: 0, resolved: 0, unresolved: 0, ambiguous: 0, diagnostics: 0 }, nodes: [], relationships: [], diagnostics: [] };
    const emptyLayout = layoutRelationshipGraph(empty);
    assert.deepEqual(emptyLayout.nodes, []);
    assert.ok(emptyLayout.width > 0 && emptyLayout.height > 0);

    const disconnected = report(false);
    const layout = layoutRelationshipGraph({ ...disconnected, relationships: [], summary: { ...disconnected.summary, relationships: 0, resolved: 0 } });
    assert.deepEqual(layout.nodes.map(({ x }) => x), [40, 40, 40]);
    assert.equal(new Set(layout.nodes.map(({ y }) => y)).size, 3);
  });

  it('filters status and relationship type without inventing relationships', () => {
    const value = report(false);
    const filtered = layoutRelationshipGraph(value, { query: '', status: 'Accepted', type: 'depends-on', focusPath: null });
    assert.deepEqual(filtered.edges.map((edge) => edge.type), ['depends-on']);
    assert.deepEqual(filtered.nodes.map((node) => node.id), ['ADR-1', 'ADR-2']);
    assert.deepEqual(relationshipGraphStatuses(value), ['Accepted']);
  });

  it('focuses the selected ADR and direct neighbors only', () => {
    const focused = layoutRelationshipGraph(report(false), { query: '', status: null, type: null, focusPath: 'docs/adr/2.md' });
    assert.deepEqual(focused.nodes.map((node) => node.path), ['docs/adr/2.md', 'docs/adr/1.md', 'docs/adr/3.md']);
    const leaf = layoutRelationshipGraph(report(false), { query: '', status: null, type: null, focusPath: 'docs/adr/1.md' });
    assert.deepEqual(leaf.nodes.map((node) => node.path), ['docs/adr/1.md', 'docs/adr/2.md']);
  });

  it('caps visible nodes after filtering and reports truncation', () => {
    const value = report(false);
    const nodes = Array.from({ length: 1_000 }, (_, index) => ({ id: `ADR-${index}`, title: null, status: index % 2 === 0 ? 'Accepted' : 'Proposed', path: `docs/adr/${index}.md`, format: 'canonical' as const }));
    const large: RelationshipGraphReport = { ...value, nodes, relationships: [], diagnostics: [], summary: { nodes: nodes.length, relationships: 0, resolved: 0, unresolved: 0, ambiguous: 0, diagnostics: 0 } };
    const layout = layoutRelationshipGraph(large, { query: '', status: null, type: null, focusPath: null });
    assert.equal(layout.nodes.length, maximumVisibleRelationshipGraphNodes);
    assert.equal(layout.totalMatchingNodes, nodes.length);
    assert.equal(layout.truncated, true);
    const filtered = layoutRelationshipGraph(large, { query: '', status: 'Proposed', type: null, focusPath: null });
    assert.equal(filtered.nodes.length, maximumVisibleRelationshipGraphNodes);
    assert.equal(filtered.totalMatchingNodes, 500);
    assert.equal(filtered.truncated, true);
    const searched = layoutRelationshipGraph(large, { query: 'docs/adr/999.md', status: null, type: null, focusPath: null });
    assert.deepEqual(searched.nodes.map((node) => node.path), ['docs/adr/999.md']);
    assert.equal(searched.truncated, false);
  });

  it('marks cycles, broken references, and ambiguous declarations from existing evidence', () => {
    const base = report(false);
    const relationships: RelationshipGraphReport['relationships'] = [
      ...base.relationships,
      { sourceId: 'ADR-3', sourcePath: 'docs/adr/3.md', type: 'supersedes', declaredTarget: 'ADR-1', resolution: 'resolved', targets: [{ id: 'ADR-1', path: 'docs/adr/1.md' }] },
      { sourceId: 'ADR-1', sourcePath: 'docs/adr/1.md', type: 'supersedes', declaredTarget: 'ADR-3', resolution: 'resolved', targets: [{ id: 'ADR-3', path: 'docs/adr/3.md' }] },
      { sourceId: 'ADR-1', sourcePath: 'docs/adr/1.md', type: 'depends-on', declaredTarget: 'missing', resolution: 'unresolved', targets: [] },
      { sourceId: 'ADR-2', sourcePath: 'docs/adr/2.md', type: 'depends-on', declaredTarget: 'duplicate', resolution: 'ambiguous', targets: [{ id: 'ADR-1', path: 'docs/adr/1.md' }, { id: 'ADR-3', path: 'docs/adr/3.md' }] },
    ];
    const value: RelationshipGraphReport = { ...base, relationships, diagnostics: [{ code: 'ADR010', message: 'cycle', path: 'docs/adr/3.md', severity: 'error' }, { code: 'ADR010', message: 'cycle', path: 'docs/adr/1.md', severity: 'error' }], summary: { nodes: 3, relationships: relationships.length, resolved: 4, unresolved: 1, ambiguous: 1, diagnostics: 2 } };
    const layout = layoutRelationshipGraph(value);
    assert.equal(layout.nodes.find((node) => node.path === 'docs/adr/1.md')?.broken, true);
    assert.equal(layout.nodes.find((node) => node.path === 'docs/adr/2.md')?.ambiguous, true);
    assert.equal(layout.nodes.find((node) => node.path === 'docs/adr/3.md')?.cycle, true);
    assert.equal(layout.edges.some((edge) => edge.cycle), true);
  });

  it('marks only edges that participate in a supersession cycle', () => {
    const nodes = [1, 2, 3, 4].map((id) => ({ id: `ADR-${id}`, title: null, status: 'Superseded', path: `${id}.md`, format: 'canonical' as const }));
    const relationships: RelationshipGraphReport['relationships'] = [
      resolvedEdge(1, 2), resolvedEdge(2, 1), resolvedEdge(3, 4), resolvedEdge(4, 3),
      resolvedEdge(2, 3), resolvedEdge(3, 2, 'superseded-by'),
    ];
    const diagnostics = nodes.map((node) => ({ code: 'ADR010', message: 'cycle', path: node.path, severity: 'error' as const }));
    const value: RelationshipGraphReport = { schemaVersion: '1.0', adrDirectory: '.', summary: { nodes: 4, relationships: 6, resolved: 6, unresolved: 0, ambiguous: 0, diagnostics: 4 }, nodes, relationships, diagnostics };
    const layout = layoutRelationshipGraph(value);
    assert.equal(layout.edges.find((item) => item.sourcePath === '2.md' && item.targetPath === '3.md')?.cycle, false);
    assert.equal(layout.edges.find((item) => item.sourcePath === '3.md' && item.targetPath === '2.md')?.cycle, false);
    assert.equal(layout.edges.filter((item) => item.cycle).length, 4);
  });
});

function report(reverse: boolean): RelationshipGraphReport {
  const nodes: RelationshipGraphReport['nodes'] = [1, 2, 3].map((id) => ({ id: `ADR-${id}`, title: `Decision ${id}`, status: 'Accepted', path: `docs/adr/${id}.md`, format: 'canonical' as const }));
  const relationships: RelationshipGraphReport['relationships'] = [
    { sourceId: 'ADR-1', sourcePath: 'docs/adr/1.md', type: 'depends-on', declaredTarget: 'ADR-2', resolution: 'resolved', targets: [{ id: 'ADR-2', path: 'docs/adr/2.md' }] },
    { sourceId: 'ADR-2', sourcePath: 'docs/adr/2.md', type: 'superseded-by', declaredTarget: 'ADR-3', resolution: 'resolved', targets: [{ id: 'ADR-3', path: 'docs/adr/3.md' }] },
  ];
  return { schemaVersion: '1.0', adrDirectory: 'docs/adr', summary: { nodes: 3, relationships: 2, resolved: 2, unresolved: 0, ambiguous: 0, diagnostics: 0 }, nodes: reverse ? [...nodes].reverse() : nodes, relationships: reverse ? [...relationships].reverse() : relationships, diagnostics: [] };
}

function resolvedEdge(source: number, target: number, type: 'supersedes' | 'superseded-by' = 'supersedes'): RelationshipGraphReport['relationships'][number] {
  return { sourceId: `ADR-${source}`, sourcePath: `${source}.md`, type, declaredTarget: `ADR-${target}`, resolution: 'resolved', targets: [{ id: `ADR-${target}`, path: `${target}.md` }] };
}
