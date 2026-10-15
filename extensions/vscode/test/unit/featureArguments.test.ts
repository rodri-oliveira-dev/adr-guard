import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { baselineArguments, catalogArguments, draftArguments, driftArguments, impactArguments, reviewArguments } from '../../src/commands/featureArguments';

describe('visual feature CLI arguments', () => {
  it('builds impact and drift JSON invocations as literal arrays', () => {
    assert.deepEqual(impactArguments('/repo', 'origin/main;echo unsafe', '/repo/map.json'), ['impact', '/repo', '--base-ref', 'origin/main;echo unsafe', '--map', '/repo/map.json', '--format', 'json']);
    assert.deepEqual(driftArguments('/repo', '/repo/rules.json', ['/repo/a.json', '/repo/b.json'], '/repo/base.json', 'enforce'), ['drift', '/repo', '--rules', '/repo/rules.json', '--evidence', '/repo/a.json', '--evidence', '/repo/b.json', '--baseline', '/repo/base.json', '--format', 'json', '--policy', 'enforce']);
  });

  it('adds overwrite and enriched catalog flags only for explicit choices', () => {
    assert.deepEqual(baselineArguments('/repo/docs/adr', '/repo/baseline.json', false), ['baseline', '/repo/docs/adr', '--output', '/repo/baseline.json']);
    assert.deepEqual(baselineArguments('/repo/docs/adr', '/repo/baseline.json', true).at(-1), '--update');
    assert.deepEqual(catalogArguments('/repo/docs/adr', 'standard'), ['index', '/repo/docs/adr']);
    assert.deepEqual(catalogArguments('/repo/docs/adr', 'enriched'), ['index', '/repo/docs/adr', '--catalog', 'enriched']);
  });

  it('keeps provider context explicit and separates preview from creation', () => {
    const provider = { provider: 'openai-compatible' as const, model: 'model', endpoint: 'https://example.test/v1', contextFiles: ['/repo/context.md'], includeExistingAdrs: true };
    const preview = draftArguments('/repo/docs/adr', 'Title', 'Context', 'pt-BR', 'minimal', provider, true);
    assert.ok(preview.includes('--preview'));
    assert.deepEqual(preview.slice(preview.indexOf('--context-file'), preview.indexOf('--context-file') + 2), ['--context-file', '/repo/context.md']);
    assert.equal(draftArguments('/repo/docs/adr', 'Title', 'Context', 'pt-BR', 'minimal', provider, false).includes('--preview'), false);
    assert.ok(reviewArguments('/repo/docs/adr/0001.md', 'advisory', '/repo/policy.json', 'origin/main', provider).includes('--compare-ref'));
  });
});
