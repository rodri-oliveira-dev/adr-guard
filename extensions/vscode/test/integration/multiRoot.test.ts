import assert from 'node:assert/strict';
import type { OperationalLog } from '../../src/logging';
import { AdrExplorerProvider, AdrNode, WorkspaceNode } from '../../src/explorer/provider';

suite('ADR Guard Explorer multi-root', () => {
  test('groups workspace folders and presents a clear empty state', async () => {
    const provider = new AdrExplorerProvider({ info: () => undefined } as unknown as OperationalLog);
    try {
      const roots = await provider.getChildren();
      assert.equal(roots.length, 2);
      assert.ok(roots.every((item) => item instanceof WorkspaceNode));
      const populated = roots.find((item) => item instanceof WorkspaceNode && item.folder.name === 'with-adrs');
      const empty = roots.find((item) => item instanceof WorkspaceNode && item.folder.name === 'empty-adrs');
      assert.ok(populated instanceof WorkspaceNode);
      assert.ok(empty instanceof WorkspaceNode);
      const populatedChildren = await provider.getChildren(populated);
      assert.equal(populatedChildren.length, 1);
      assert.ok(populatedChildren[0] instanceof AdrNode);
      const emptyChildren = await provider.getChildren(empty);
      assert.equal(emptyChildren.length, 1);
      const label = emptyChildren[0]?.label;
      assert.match(typeof label === 'string' ? label : label?.label ?? '', /No ADR Markdown files/u);
    } finally {
      provider.dispose();
    }
  });
});
