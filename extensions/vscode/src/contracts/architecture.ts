export interface ImpactReport {
  readonly schemaVersion: '1.0';
  readonly advisory: true;
  readonly repository: { readonly baseReference: string; readonly mergeBase: string; readonly manifestPath: string };
  readonly summary: { readonly totalChanges: number; readonly affectedDecisions: number; readonly unknownDecisions: number; readonly notMatchedDecisions: number };
  readonly coverage: { readonly affectedChanges: number; readonly unknownChanges: number; readonly notMatchedChanges: number };
  readonly uncertainty: { readonly hasUnknowns: boolean; readonly decisionStableIds: readonly string[]; readonly explanation: string };
  readonly changedFiles: readonly ImpactChange[];
  readonly decisions: readonly ImpactDecision[];
}

export interface ImpactChange {
  readonly scope: string;
  readonly changeType: string;
  readonly oldPath: string | null;
  readonly newPath: string | null;
  readonly similarity: number | null;
  readonly status: 'affected' | 'unknown' | 'not-matched';
  readonly decisionStableIds: readonly string[];
}

export interface ImpactDecision {
  readonly stableId: string;
  readonly path: string;
  readonly title: string | null;
  readonly lifecycleStatus: string | null;
  readonly resolution: string;
  readonly status: 'affected' | 'unknown' | 'not-matched';
  readonly uncertainty: string | null;
  readonly evidence: readonly { readonly scope: string; readonly changeType: string; readonly pathRole: string; readonly path: string; readonly pattern: string; readonly relationship: string; readonly reason: string }[];
}

export interface DriftReport {
  readonly schemaVersion: '1.0';
  readonly policy: 'advisory' | 'enforce';
  readonly repository: { readonly rulesManifest: string; readonly commit: string | null };
  readonly coverage: { readonly totalRules: number; readonly pass: number; readonly violations: number; readonly unknown: number; readonly notApplicable: number };
  readonly findings: readonly DriftFinding[];
}

export interface DriftFinding {
  readonly fingerprint: string;
  readonly ruleId: string;
  readonly adrStableId: string;
  readonly outcome: 'pass' | 'violation' | 'unknown' | 'not-applicable';
  readonly checkType: string;
  readonly confidence: string;
  readonly baselineState: string;
  readonly explanation: string;
  readonly evidence: readonly { readonly kind: string; readonly source: string; readonly subject: string | null; readonly target: string | null; readonly line: number | null; readonly detail: string }[];
}

export interface ReviewReport {
  readonly schemaVersion: '1.0';
  readonly generatedAtUtc: string;
  readonly target: { readonly sourceId: string; readonly path: string; readonly adrId: string | null; readonly title: string | null; readonly status: string | null };
  readonly reviewer: { readonly name: string; readonly contractVersion: string };
  readonly provider: { readonly name: string; readonly model: string };
  readonly inputScope: { readonly includeExistingAdrs: boolean; readonly explicitContext: readonly ReviewSource[]; readonly existingAdrs: readonly ReviewSource[]; readonly existingAdrSelectionBounded: boolean };
  readonly outcome: string;
  readonly dimensions: readonly ReviewDimension[];
  readonly findings: readonly ReviewFinding[];
  readonly limitations: readonly string[];
  readonly costCaveat: string;
}

interface ReviewSource { readonly sourceId: string; readonly path: string; readonly adrId: string | null }
interface ReviewEvidence extends ReviewSource { readonly line: number | null; readonly excerpt: string | null }
interface ReviewAssessment { readonly classification: string; readonly explanation: string; readonly guidance: string; readonly followUpPriority: string | null; readonly uncertainty: string | null; readonly evidence: readonly ReviewEvidence[] }
export interface ReviewDimension { readonly name: string; readonly assessments: readonly ReviewAssessment[] }
export interface ReviewFinding extends ReviewAssessment { readonly dimension: string; readonly followUpPriority: string }

const maximumContractCharacters = 1_048_576;

export function parseImpactReport(json: string): ImpactReport {
  const value = parseRoot(json, 'impact');
  requireExactKeys(value, ['schemaVersion', 'advisory', 'repository', 'summary', 'coverage', 'uncertainty', 'changedFiles', 'decisions']);
  if (value.schemaVersion !== '1.0' || value.advisory !== true || !isRecord(value.repository)
    || !isRecord(value.summary) || !isRecord(value.coverage) || !isRecord(value.uncertainty)
    || !Array.isArray(value.changedFiles) || !Array.isArray(value.decisions)) invalid('impact');
  const report = value as unknown as ImpactReport;
  requireExactKeys(value.repository, ['baseReference', 'mergeBase', 'manifestPath']);
  requireExactKeys(value.summary, ['totalChanges', 'affectedDecisions', 'unknownDecisions', 'notMatchedDecisions']);
  requireExactKeys(value.coverage, ['affectedChanges', 'unknownChanges', 'notMatchedChanges']);
  requireExactKeys(value.uncertainty, ['hasUnknowns', 'decisionStableIds', 'explanation']);
  if (!numbers(report.summary.totalChanges, report.summary.affectedDecisions, report.summary.unknownDecisions, report.summary.notMatchedDecisions,
    report.coverage.affectedChanges, report.coverage.unknownChanges, report.coverage.notMatchedChanges)
    || !strings(report.repository.baseReference, report.repository.mergeBase, report.repository.manifestPath, report.uncertainty.explanation)
    || typeof report.uncertainty.hasUnknowns !== 'boolean' || !stringArray(report.uncertainty.decisionStableIds)
    || !report.changedFiles.every(validImpactChange) || !report.decisions.every(validImpactDecision)) invalid('impact');
  return report;
}

export function parseDriftReport(json: string): DriftReport {
  const value = parseRoot(json, 'drift');
  requireExactKeys(value, ['schemaVersion', 'policy', 'repository', 'coverage', 'findings']);
  if (value.schemaVersion !== '1.0' || (value.policy !== 'advisory' && value.policy !== 'enforce')
    || !isRecord(value.repository) || !isRecord(value.coverage) || !Array.isArray(value.findings)) invalid('drift');
  const report = value as unknown as DriftReport;
  requireExactKeys(value.repository, ['rulesManifest', 'commit']);
  requireExactKeys(value.coverage, ['totalRules', 'pass', 'violations', 'unknown', 'notApplicable']);
  if (!strings(report.repository.rulesManifest) || (report.repository.commit !== null && typeof report.repository.commit !== 'string')
    || !numbers(report.coverage.totalRules, report.coverage.pass, report.coverage.violations, report.coverage.unknown, report.coverage.notApplicable)
    || !report.findings.every(validDriftFinding)) invalid('drift');
  return report;
}

export function parseReviewReport(json: string): ReviewReport {
  const value = parseRoot(json, 'review');
  requireExactKeys(value, ['schemaVersion', 'generatedAtUtc', 'target', 'reviewer', 'provider', 'inputScope', 'outcome', 'dimensions', 'findings', 'limitations', 'costCaveat']);
  if (value.schemaVersion !== '1.0' || !isRecord(value.target) || !isRecord(value.reviewer) || !isRecord(value.provider) || !isRecord(value.inputScope)
    || !Array.isArray(value.dimensions) || value.dimensions.length !== 8 || !Array.isArray(value.findings)
    || !stringArray(value.limitations) || !strings(value.generatedAtUtc, value.outcome, value.costCaveat)) invalid('review');
  const report = value as unknown as ReviewReport;
  requireExactKeys(value.target, ['sourceId', 'path', 'adrId', 'title', 'status']);
  requireExactKeys(value.reviewer, ['name', 'contractVersion']);
  requireExactKeys(value.provider, ['name', 'model']);
  requireExactKeys(value.inputScope, ['includeExistingAdrs', 'explicitContext', 'existingAdrs', 'existingAdrSelectionBounded']);
  if (!strings(report.target.sourceId, report.target.path, report.provider.name, report.provider.model)
    || !strings(report.reviewer.name, report.reviewer.contractVersion)
    || typeof report.inputScope.includeExistingAdrs !== 'boolean' || !Array.isArray(report.inputScope.explicitContext)
    || !Array.isArray(report.inputScope.existingAdrs) || typeof report.inputScope.existingAdrSelectionBounded !== 'boolean'
    || !report.inputScope.explicitContext.every(validReviewSource) || !report.inputScope.existingAdrs.every(validReviewSource)
    || !report.dimensions.every(validReviewDimension) || !report.findings.every((item) => validReviewAssessment(item, true))) invalid('review');
  return report;
}

function validImpactChange(value: unknown): boolean {
  if (!isRecord(value)) return false;
  requireExactKeys(value, ['scope', 'changeType', 'oldPath', 'newPath', 'similarity', 'status', 'decisionStableIds']);
  return strings(value.scope, value.changeType, value.status)
    && ['affected', 'unknown', 'not-matched'].includes(value.status as string)
    && nullableString(value.oldPath) && nullableString(value.newPath)
    && (value.similarity === null || (typeof value.similarity === 'number' && Number.isInteger(value.similarity) && value.similarity >= 0 && value.similarity <= 100))
    && stringArray(value.decisionStableIds);
}

function validImpactDecision(value: unknown): boolean {
  if (!isRecord(value)) return false;
  requireExactKeys(value, ['stableId', 'path', 'title', 'lifecycleStatus', 'resolution', 'status', 'uncertainty', 'evidence']);
  return strings(value.stableId, value.path, value.resolution, value.status)
    && ['affected', 'unknown', 'not-matched'].includes(value.status as string)
    && nullableString(value.title) && nullableString(value.lifecycleStatus) && nullableString(value.uncertainty)
    && Array.isArray(value.evidence) && value.evidence.every((item) => {
      if (!isRecord(item)) return false;
      requireExactKeys(item, ['scope', 'changeType', 'pathRole', 'path', 'pattern', 'relationship', 'reason']);
      return strings(item.scope, item.changeType, item.pathRole, item.path, item.pattern, item.relationship, item.reason);
    });
}

function validDriftFinding(value: unknown): boolean {
  if (!isRecord(value)) return false;
  requireExactKeys(value, ['fingerprint', 'ruleId', 'adrStableId', 'outcome', 'checkType', 'confidence', 'baselineState', 'explanation', 'evidence']);
  return strings(value.fingerprint, value.ruleId, value.adrStableId, value.outcome, value.checkType, value.confidence, value.baselineState, value.explanation)
    && ['pass', 'violation', 'unknown', 'not-applicable'].includes(value.outcome as string)
    && Array.isArray(value.evidence) && value.evidence.every((item) => {
      if (!isRecord(item)) return false;
      requireExactKeys(item, ['kind', 'source', 'subject', 'target', 'line', 'detail']);
      return strings(item.kind, item.source, item.detail) && nullableString(item.subject) && nullableString(item.target)
        && (item.line === null || (typeof item.line === 'number' && Number.isInteger(item.line) && item.line > 0));
    });
}

function validReviewSource(value: unknown): boolean {
  if (!isRecord(value)) return false;
  requireExactKeys(value, ['sourceId', 'path', 'adrId']);
  return strings(value.sourceId, value.path) && nullableString(value.adrId);
}
function validReviewDimension(value: unknown): boolean {
  if (!isRecord(value)) return false;
  requireExactKeys(value, ['name', 'assessments']);
  return typeof value.name === 'string' && Array.isArray(value.assessments)
    && value.assessments.every((item) => validReviewAssessment(item, false));
}
function validReviewAssessment(value: unknown, finding: boolean): boolean {
  if (!isRecord(value)) return false;
  requireExactKeys(value, [...(finding ? ['dimension'] : []), 'classification', 'followUpPriority', 'uncertainty', 'explanation', 'guidance', 'evidence']);
  return (!finding || typeof value.dimension === 'string') && strings(value.classification, value.explanation, value.guidance)
    && nullableString(value.followUpPriority) && nullableString(value.uncertainty) && Array.isArray(value.evidence)
    && value.evidence.every((item) => {
      if (!isRecord(item)) return false;
      requireExactKeys(item, ['sourceId', 'path', 'adrId', 'line', 'excerpt']);
      return strings(item.sourceId, item.path) && nullableString(item.adrId) && nullableString(item.excerpt)
        && (item.line === null || (typeof item.line === 'number' && Number.isInteger(item.line) && item.line > 0));
    });
}

function parseRoot(json: string, contract: string): Record<string, unknown> {
  if (json.length === 0 || json.length > maximumContractCharacters) invalid(contract);
  try { const value: unknown = JSON.parse(json); if (isRecord(value)) return value; } catch { /* translated below */ }
  return invalid(contract);
}
function requireExactKeys(value: Record<string, unknown>, keys: readonly string[]): void { if (Object.keys(value).some((key) => !keys.includes(key)) || keys.some((key) => !(key in value))) invalid('CLI'); }
function nullableString(value: unknown): boolean { return value === null || typeof value === 'string'; }
function stringArray(value: unknown): value is string[] { return Array.isArray(value) && value.every((item) => typeof item === 'string'); }
function strings(...values: unknown[]): boolean { return values.every((value) => typeof value === 'string'); }
function numbers(...values: unknown[]): boolean { return values.every((value) => typeof value === 'number' && Number.isInteger(value) && value >= 0); }
function isRecord(value: unknown): value is Record<string, unknown> { return typeof value === 'object' && value !== null && !Array.isArray(value); }
function invalid(contract: string): never { throw new Error(`ADR Guard returned an invalid or unsupported ${contract} JSON contract.`); }
