import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { parseFeatureMessage } from '../../src/webview/featureMessages';

describe('visual feature message boundary', () => {
  it('accepts explicit bounded operations and keeps argument-bearing values separate', () => {
    const values = { feature: 'impact', workspace: 'file:///workspace', baseReference: 'origin/main', manifest: '.adrguard-impact.json' };
    assert.deepEqual(parseFeatureMessage({ type: 'executeFeature', values }), { type: 'executeFeature', values });
    assert.deepEqual(parseFeatureMessage({ type: 'showFeature', workspace: 'file:///workspace', feature: 'drift' }), { type: 'showFeature', workspace: 'file:///workspace', feature: 'drift' });
  });

  it('rejects traversal, unsafe Git references, insecure endpoints, unknown fields, and arbitrary features', () => {
    const draft = { feature: 'draft', workspace: 'file:///workspace', directory: 'docs/adr', title: 'Test', context: 'Context', provider: 'openai-compatible', model: 'model', endpoint: 'https://example.test/v1' };
    assert.equal(parseFeatureMessage({ type: 'executeFeature', values: { ...draft, contextFiles: ['../secret.txt'] } }), undefined);
    assert.equal(parseFeatureMessage({ type: 'executeFeature', values: { ...draft, endpoint: 'http://example.test' } }), undefined);
    assert.equal(parseFeatureMessage({ type: 'executeFeature', values: { ...draft, provider: 'openai', endpoint: 'https://example.test' } }), undefined);
    assert.equal(parseFeatureMessage({ type: 'executeFeature', values: { ...draft, endpoint: '' } }), undefined);
    assert.equal(parseFeatureMessage({ type: 'executeFeature', values: { ...draft, extra: '--danger' } }), undefined);
    assert.equal(parseFeatureMessage({ type: 'executeFeature', values: { feature: 'shell', workspace: 'file:///workspace' } }), undefined);
    assert.equal(parseFeatureMessage({ type: 'executeFeature', values: { feature: 'impact', workspace: 'file:///workspace', baseReference: '--all', manifest: '.adrguard-impact.json' } }), undefined);
  });
});
