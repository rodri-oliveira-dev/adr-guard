import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { maximumRelationshipGraphReportNodes, parseRelationshipGraphReport, RelationshipGraphContractError, RelationshipGraphReportLimitError, type RelationshipGraphReport } from '../../src/contracts/relationshipGraph';

describe('relationship graph contract', () => {
  it('strictly transforms the versioned CLI JSON without interpreting ADR content', () => {
    const report = parseRelationshipGraphReport(JSON.stringify(validReport()));
    assert.equal(report.schemaVersion, '1.0');
    assert.equal(report.nodes[0]?.title, '<script>data only</script>');
    assert.deepEqual(report.relationships[0], {
      sourceId: 'ADR-1', sourcePath: 'docs/adr/0001.md', type: 'depends-on', declaredTarget: 'ADR-2', resolution: 'resolved', targets: [{ id: 'ADR-2', path: 'docs/adr/0002.md' }],
    });
  });

  it('rejects unsupported, inconsistent, unsafe, and unexpectedly shaped reports', () => {
    const base = validReport();
    const cases = [
      { ...base, schemaVersion: '2.0' },
      { ...base, unexpected: true },
      { ...base, summary: { ...base.summary, nodes: 99 } },
      { ...base, nodes: [{ ...base.nodes[0], path: '../outside.md' }, base.nodes[1]] },
      { ...base, relationships: [{ ...base.relationships[0], resolution: 'unresolved', targets: base.relationships[0]?.targets }] },
      { ...base, relationships: [{ ...base.relationships[0], targets: [{ id: 'ADR-3', path: 'docs/adr/0003.md' }] }] },
    ];
    for (const value of cases) assert.throws(() => parseRelationshipGraphReport(JSON.stringify(value)), RelationshipGraphContractError);
  });

  it('accepts reports larger than the visual limit and enforces a bounded contract limit', () => {
    const acceptedNodes = Array.from({ length: 101 }, (_, index) => ({ id: `ADR-${index}`, title: null, status: null, path: `docs/adr/${index}.md`, format: 'canonical' }));
    const accepted = { schemaVersion: '1.0', adrDirectory: 'docs/adr', summary: { nodes: acceptedNodes.length, relationships: 0, resolved: 0, unresolved: 0, ambiguous: 0, diagnostics: 0 }, nodes: acceptedNodes, relationships: [], diagnostics: [] };
    assert.equal(parseRelationshipGraphReport(JSON.stringify(accepted)).nodes.length, 101);
    const nodes = Array.from({ length: maximumRelationshipGraphReportNodes + 1 }, (_, index) => ({ id: `ADR-${index}`, title: null, status: null, path: `docs/adr/${index}.md`, format: 'canonical' }));
    const value = { schemaVersion: '1.0', adrDirectory: 'docs/adr', summary: { nodes: nodes.length, relationships: 0, resolved: 0, unresolved: 0, ambiguous: 0, diagnostics: 0 }, nodes, relationships: [], diagnostics: [] };
    assert.throws(() => parseRelationshipGraphReport(JSON.stringify(value)), RelationshipGraphReportLimitError);
  });
});

function validReport(): RelationshipGraphReport {
  return {
    schemaVersion: '1.0', adrDirectory: 'docs/adr',
    summary: { nodes: 2, relationships: 1, resolved: 1, unresolved: 0, ambiguous: 0, diagnostics: 1 },
    nodes: [
      { id: 'ADR-1', title: '<script>data only</script>', status: 'Accepted', path: 'docs/adr/0001.md', format: 'canonical' },
      { id: 'ADR-2', title: 'Target', status: null, path: 'docs/adr/0002.md', format: 'canonical' },
    ],
    relationships: [{ sourceId: 'ADR-1', sourcePath: 'docs/adr/0001.md', type: 'depends-on', declaredTarget: 'ADR-2', resolution: 'resolved', targets: [{ id: 'ADR-2', path: 'docs/adr/0002.md' }] }],
    diagnostics: [{ code: 'ADR010', message: '<img src=x onerror=alert(1)>', path: 'docs/adr/0001.md', severity: 'error', line: 4, column: 2 }],
  };
}
