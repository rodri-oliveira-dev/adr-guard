import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { displayId, parseAdrDisplayMetadata } from '../../src/explorer/metadata';

describe('ADR Explorer display metadata', () => {
  it('extracts canonical title, status, ID and documented relationship links', () => {
    const metadata = parseAdrDisplayMetadata('0012-use-postgres.md', `
# Use PostgreSQL
## Status
Accepted
## Context
[Narrative](0099-ignore.md)
## Depends on
[ADR 4](../adr/0004-platform.md)
## Supersedes
[Old decision](0002-old.md "history")
`, 'canonical');
    assert.equal(metadata.id, '0012');
    assert.equal(metadata.title, 'Use PostgreSQL');
    assert.equal(metadata.status, 'Accepted');
    assert.deepEqual(metadata.relationships.map(({ kind, destination }) => [kind, destination]), [
      ['depends-on', '../adr/0004-platform.md'],
      ['supersedes', '0002-old.md'],
    ]);
  });

  it('uses MADR front matter status and deterministic supersession metadata', () => {
    const metadata = parseAdrDisplayMetadata('0020-new.md', `---
status: "superseded by ADR-0042"
---
# New choice
## Status
Narrative only
## Decision Outcome
Chosen option.
`, 'madr-4');
    assert.equal(metadata.status, 'superseded by ADR-0042');
    assert.deepEqual(metadata.relationships, [{
      kind: 'superseded-by', label: 'ADR-0042', targetId: '0042',
    }]);
  });

  it('normalizes numeric IDs for unambiguous MADR relationship matching', () => {
    assert.equal(parseAdrDisplayMetadata('42-target.md', '# Target', 'madr-4').id, '0042');
  });

  it('does not infer missing metadata or links inside code fences', () => {
    const metadata = parseAdrDisplayMetadata('notes.md', `## References
\`\`\`md
[Not a relationship](0001-no.md)
\`\`\`
`, 'canonical');
    assert.equal(metadata.id, undefined);
    assert.equal(metadata.title, undefined);
    assert.equal(metadata.status, undefined);
    assert.deepEqual(metadata.relationships, []);
    assert.equal(displayId('notes.md', undefined), 'notes');
  });
});
