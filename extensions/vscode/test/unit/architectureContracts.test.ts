import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { describe, it } from 'node:test';
import { parseDriftReport, parseImpactReport, parseReviewReport } from '../../src/contracts/architecture';

describe('architecture CLI contracts', () => {
  it('parses the checked-in impact v1 fixture and rejects unknown fields or versions', async () => {
    const fixture = await readFile(path.resolve(__dirname, '../../../../../tests/AdrGuard.Tests/Fixtures/Impact/case-study/expected/impact-report-v1.json'), 'utf8');
    const report = parseImpactReport(fixture);
    assert.equal(report.summary.affectedDecisions, 2);
    assert.equal(report.coverage.notMatchedChanges, 2);
    assert.equal(report.decisions.find((decision) => decision.status === 'unknown')?.stableId, 'ADR-3');
    assert.throws(() => parseImpactReport(JSON.stringify({ ...JSON.parse(fixture) as object, schemaVersion: '2.0' })), /invalid or unsupported/u);
    assert.throws(() => parseImpactReport(JSON.stringify({ ...JSON.parse(fixture) as object, unexpected: true })), /invalid or unsupported/u);
  });

  it('preserves all drift outcomes, baseline classification, confidence, and evidence', () => {
    const findings = ['pass', 'violation', 'unknown', 'not-applicable'].map((outcome, index) => ({
      fingerprint: `F${index}`, ruleId: `rule-${index}`, adrStableId: `ADR-${index}`, outcome,
      checkType: 'external-test', confidence: index === 1 ? 'deterministic-static' : 'reported-external',
      baselineState: index === 1 ? 'existing' : 'current', explanation: `result ${index}`,
      evidence: [{ kind: 'test', source: `evidence-${index}.json`, subject: null, target: null, line: null, detail: 'bounded' }],
    }));
    const report = parseDriftReport(JSON.stringify({ schemaVersion: '1.0', policy: 'advisory', repository: { rulesManifest: '.adrguard-fitness.json', commit: null }, coverage: { totalRules: 4, pass: 1, violations: 1, unknown: 1, notApplicable: 1 }, findings }));
    assert.deepEqual(report.findings.map((finding) => finding.outcome), ['pass', 'violation', 'unknown', 'not-applicable']);
    assert.equal(report.findings[1]?.baselineState, 'existing');
  });

  it('requires the versioned review contract and all eight dimensions', () => {
    const dimensions = Array.from({ length: 8 }, (_, index) => ({ name: `dimension-${index}`, assessments: [] }));
    const value = { schemaVersion: '1.0', generatedAtUtc: '2026-01-01T00:00:00Z', target: { sourceId: 'target', path: '0001-test.md', adrId: '0001', title: 'Test', status: 'Proposed' }, reviewer: { name: 'adr-guard', contractVersion: '1.0' }, provider: { name: 'openai', model: 'explicit' }, inputScope: { includeExistingAdrs: false, explicitContext: [], existingAdrs: [], existingAdrSelectionBounded: false }, outcome: 'no-follow-up-findings', dimensions, findings: [], limitations: ['Advisory'], costCaveat: 'Costs may apply.' };
    assert.equal(parseReviewReport(JSON.stringify(value)).dimensions.length, 8);
    assert.throws(() => parseReviewReport(JSON.stringify({ ...value, dimensions: dimensions.slice(0, 7) })), /invalid or unsupported/u);
  });
});
