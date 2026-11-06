import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { describe, it } from 'node:test';
import { relationshipGraphWebviewHtml } from '../../src/webview/relationshipGraphHtml';

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
});
