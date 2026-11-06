import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import type { RelationshipGraphReport } from '../../src/contracts/relationshipGraph';
import { layoutRelationshipGraph } from '../../src/webview/relationshipGraphModel';

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
});

function report(reverse: boolean): RelationshipGraphReport {
  const nodes: RelationshipGraphReport['nodes'] = [1, 2, 3].map((id) => ({ id: `ADR-${id}`, title: `Decision ${id}`, status: 'Accepted', path: `docs/adr/${id}.md`, format: 'canonical' as const }));
  const relationships: RelationshipGraphReport['relationships'] = [
    { sourceId: 'ADR-1', sourcePath: 'docs/adr/1.md', type: 'depends-on', declaredTarget: 'ADR-2', resolution: 'resolved', targets: [{ id: 'ADR-2', path: 'docs/adr/2.md' }] },
    { sourceId: 'ADR-2', sourcePath: 'docs/adr/2.md', type: 'superseded-by', declaredTarget: 'ADR-3', resolution: 'resolved', targets: [{ id: 'ADR-3', path: 'docs/adr/3.md' }] },
  ];
  return { schemaVersion: '1.0', adrDirectory: 'docs/adr', summary: { nodes: 3, relationships: 2, resolved: 2, unresolved: 0, ambiguous: 0, diagnostics: 0 }, nodes: reverse ? [...nodes].reverse() : nodes, relationships: reverse ? [...relationships].reverse() : relationships, diagnostics: [] };
}
