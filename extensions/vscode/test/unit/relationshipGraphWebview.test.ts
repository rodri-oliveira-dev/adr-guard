import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { describe, it } from 'node:test';
import { relationshipGraphWebviewHtml } from '../../src/webview/relationshipGraphHtml';
import { parseRelationshipGraphMessage } from '../../src/webview/relationshipGraphMessages';

describe('relationship graph Webview security', () => {
  it('uses a restrictive CSP, nonce-protected local script, and escaped attributes', () => {
    const html = relationshipGraphWebviewHtml('vscode-webview:', 'local-script.js', 'local-style.css', 'en-US" onload=alert(1)', 'safe-nonce');
    assert.match(html, /default-src 'none'; img-src 'none'; font-src 'none'; connect-src 'none';/u);
    assert.match(html, /script-src 'nonce-safe-nonce'/u);
    assert.match(html, /<script nonce="safe-nonce" src="local-script\.js"><\/script>/u);
    assert.match(html, /lang="en-US&quot; onload=alert\(1\)"/u);
    assert.doesNotMatch(html, /lang="en-US" onload=/u);
    assert.doesNotMatch(html, /https?:\/\//u);
  });

  it('renders untrusted contract fields only with text DOM APIs', async () => {
    const script = await readFile(path.resolve(__dirname, '../../../media/relationshipGraph.js'), 'utf8');
    assert.doesNotMatch(script, /\.innerHTML|insertAdjacentHTML|eval\s*\(|new Function/u);
    assert.match(script, /textContent/u);
    assert.match(script, /createElementNS/u);
  });

  it('provides selection, filtering, explicit refresh, focus, and keyboard navigation without edit actions', async () => {
    const script = await readFile(path.resolve(__dirname, '../../../media/relationshipGraph.js'), 'utf8');
    assert.match(script, /type: 'filter'/u);
    assert.match(script, /type: 'focus'/u);
    assert.match(script, /type: 'openAdr'/u);
    assert.match(script, /ArrowRight/u);
    assert.match(script, /aria-pressed/u);
    assert.match(script, /tabindex/u);
    assert.doesNotMatch(script, /createAdr|approveAdr|updateStatus|saveAdr/u);
  });

  it('strictly bounds extension-host messages and rejects unknown shapes', () => {
    assert.deepEqual(parseRelationshipGraphMessage({ type: 'ready' }), { type: 'ready' });
    assert.deepEqual(parseRelationshipGraphMessage({ type: 'openAdr', path: 'docs/adr/0001.md' }), { type: 'openAdr', path: 'docs/adr/0001.md' });
    assert.deepEqual(parseRelationshipGraphMessage({ type: 'filter', status: null, relationshipType: 'depends-on' }), { type: 'filter', status: null, relationshipType: 'depends-on' });
    for (const invalid of [
      null, [], { type: 'ready', extra: true }, { type: 'openAdr', path: '' }, { type: 'openAdr', path: 'x'.repeat(4_097) },
      { type: 'focus' }, { type: 'filter', status: '', relationshipType: null }, { type: 'filter', status: null, relationshipType: 'inferred' },
    ]) assert.equal(parseRelationshipGraphMessage(invalid), undefined);
  });

  it('uses editor theme variables for light and dark compatibility', async () => {
    const style = await readFile(path.resolve(__dirname, '../../../media/relationshipGraph.css'), 'utf8');
    assert.match(style, /color-scheme: light dark/u);
    assert.match(style, /var\(--vscode-editor-background\)/u);
    assert.match(style, /var\(--vscode-focusBorder\)/u);
    assert.doesNotMatch(style, /#[0-9a-f]{3,8}\b/iu);
  });
});
