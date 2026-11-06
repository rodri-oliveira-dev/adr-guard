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
    assert.equal(layout.edges.at(-1)?.resolution, 'unresolved');
  });

  it('filters status and relationship type without inventing relationships', () => {
    const value = report(false);
    const filtered = layoutRelationshipGraph(value, { status: 'Accepted', type: 'depends-on', focusPath: null });
    assert.deepEqual(filtered.edges.map((edge) => edge.type), ['depends-on']);
    assert.deepEqual(filtered.nodes.map((node) => node.id), ['ADR-1', 'ADR-2']);
    assert.deepEqual(relationshipGraphStatuses(value), ['Accepted']);
  });

  it('focuses the selected ADR and direct neighbors only', () => {
    const focused = layoutRelationshipGraph(report(false), { status: null, type: null, focusPath: 'docs/adr/2.md' });
    assert.deepEqual(focused.nodes.map((node) => node.path), ['docs/adr/2.md', 'docs/adr/1.md', 'docs/adr/3.md']);
    const leaf = layoutRelationshipGraph(report(false), { status: null, type: null, focusPath: 'docs/adr/1.md' });
    assert.deepEqual(leaf.nodes.map((node) => node.path), ['docs/adr/1.md', 'docs/adr/2.md']);
  });

  it('caps visible nodes after filtering and reports truncation', () => {
    const value = report(false);
    const nodes = Array.from({ length: maximumVisibleRelationshipGraphNodes + 5 }, (_, index) => ({ id: `ADR-${index}`, title: null, status: index % 2 === 0 ? 'Accepted' : 'Proposed', path: `docs/adr/${index}.md`, format: 'canonical' as const }));
    const large: RelationshipGraphReport = { ...value, nodes, relationships: [], diagnostics: [], summary: { nodes: nodes.length, relationships: 0, resolved: 0, unresolved: 0, ambiguous: 0, diagnostics: 0 } };
    const layout = layoutRelationshipGraph(large, { status: null, type: null, focusPath: null });
    assert.equal(layout.nodes.length, maximumVisibleRelationshipGraphNodes);
    assert.equal(layout.totalMatchingNodes, nodes.length);
    assert.equal(layout.truncated, true);
    const filtered = layoutRelationshipGraph(large, { status: 'Proposed', type: null, focusPath: null });
    assert.equal(filtered.nodes.length, Math.floor(nodes.length / 2));
    assert.equal(filtered.truncated, false);
  });

  it('marks cycles, broken references, and ambiguous declarations from existing evidence', () => {
    const base = report(false);
    const relationships: RelationshipGraphReport['relationships'] = [
      ...base.relationships,
      { sourceId: 'ADR-3', sourcePath: 'docs/adr/3.md', type: 'supersedes', declaredTarget: 'ADR-1', resolution: 'resolved', targets: [{ id: 'ADR-1', path: 'docs/adr/1.md' }] },
      { sourceId: 'ADR-1', sourcePath: 'docs/adr/1.md', type: 'depends-on', declaredTarget: 'missing', resolution: 'unresolved', targets: [] },
      { sourceId: 'ADR-2', sourcePath: 'docs/adr/2.md', type: 'depends-on', declaredTarget: 'duplicate', resolution: 'ambiguous', targets: [{ id: 'ADR-1', path: 'docs/adr/1.md' }, { id: 'ADR-3', path: 'docs/adr/3.md' }] },
    ];
    const value: RelationshipGraphReport = { ...base, relationships, diagnostics: [{ code: 'ADR010', message: 'cycle', path: 'docs/adr/3.md', severity: 'error' }, { code: 'ADR010', message: 'cycle', path: 'docs/adr/1.md', severity: 'error' }], summary: { nodes: 3, relationships: relationships.length, resolved: 3, unresolved: 1, ambiguous: 1, diagnostics: 2 } };
    const layout = layoutRelationshipGraph(value);
    assert.equal(layout.nodes.find((node) => node.path === 'docs/adr/1.md')?.broken, true);
    assert.equal(layout.nodes.find((node) => node.path === 'docs/adr/2.md')?.ambiguous, true);
    assert.equal(layout.nodes.find((node) => node.path === 'docs/adr/3.md')?.cycle, true);
    assert.equal(layout.edges.some((edge) => edge.cycle), true);
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
