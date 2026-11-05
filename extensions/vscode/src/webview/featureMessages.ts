import path from 'node:path';
import { isSafeGitBaseReference } from '../commands/arguments';

export type VisualFeature = 'impact' | 'drift' | 'baseline' | 'catalog' | 'draft' | 'review';
export type AiProvider = 'openai' | 'anthropic' | 'gemini' | 'openai-compatible';

export interface FeatureValues {
  readonly feature: VisualFeature;
  readonly workspace: string;
  readonly directory?: string;
  readonly baseReference?: string;
  readonly manifest?: string;
  readonly rules?: string;
  readonly evidence?: readonly string[];
  readonly baseline?: string;
  readonly destination?: string;
  readonly update?: boolean;
  readonly policy?: 'advisory' | 'enforce';
  readonly catalog?: 'standard' | 'enriched';
  readonly title?: string;
  readonly context?: string;
  readonly culture?: 'en-US' | 'pt-BR';
  readonly template?: 'configured' | 'minimal' | 'extended';
  readonly provider?: AiProvider;
  readonly model?: string;
  readonly endpoint?: string;
  readonly contextFiles?: readonly string[];
  readonly includeExistingAdrs?: boolean;
  readonly targetAdr?: string;
  readonly policyFile?: string;
  readonly compareReference?: string;
}

export interface FeatureMessage {
  readonly type: 'openFeatures' | 'showFeature' | 'executeFeature' | 'cancelFeature' | 'cancelFeatureOperation' | 'openFeatureResource' | 'createDraft';
  readonly workspace?: string;
  readonly feature?: VisualFeature;
  readonly values?: FeatureValues;
  readonly resource?: string;
}

const features: readonly VisualFeature[] = ['impact', 'drift', 'baseline', 'catalog', 'draft', 'review'];

export function parseFeatureMessage(value: unknown): FeatureMessage | undefined {
  if (!isRecord(value) || typeof value.type !== 'string') return undefined;
  switch (value.type) {
    case 'openFeatures':
    case 'cancelFeature':
    case 'cancelFeatureOperation':
      return parseWorkspaceFeatureMessage(value);
    case 'showFeature':
      return parseShowFeatureMessage(value);
    case 'openFeatureResource':
      return parseOpenResourceMessage(value);
    case 'executeFeature':
    case 'createDraft':
      return parseExecuteFeatureMessage(value);
    default:
      return undefined;
  }
}

export function parseFeatureValues(value: unknown): FeatureValues | undefined {
  if (!isRecord(value) || !isFeature(value.feature) || !bounded(value.workspace, 4096)) return undefined;
  if (!hasValidFeaturePaths(value) || !hasValidFeatureText(value)) return undefined;
  if (!hasValidFeatureSelections(value) || !hasValidProviderSettings(value)) return undefined;
  if (!optionalBoolean(value.update) || !optionalBoolean(value.includeExistingAdrs)) return undefined;
  const allowed = ['feature', 'workspace', 'directory', 'baseReference', 'manifest', 'rules', 'evidence', 'baseline', 'destination', 'update', 'policy', 'catalog', 'title', 'context', 'culture', 'template', 'provider', 'model', 'endpoint', 'contextFiles', 'includeExistingAdrs', 'targetAdr', 'policyFile', 'compareReference'];
  return exact(value, allowed) ? value as unknown as FeatureValues : undefined;
}

function parseWorkspaceFeatureMessage(value: Record<string, unknown>): FeatureMessage | undefined {
  return bounded(value.workspace, 4096) && exact(value, ['type', 'workspace'])
    ? { type: value.type as 'openFeatures' | 'cancelFeature' | 'cancelFeatureOperation', workspace: value.workspace }
    : undefined;
}

function parseShowFeatureMessage(value: Record<string, unknown>): FeatureMessage | undefined {
  if (!bounded(value.workspace, 4096) || !isFeature(value.feature)
    || (value.resource !== undefined && !safeRelative(value.resource))
    || !exact(value, ['type', 'workspace', 'feature', 'resource'])) return undefined;
  return { type: 'showFeature', workspace: value.workspace, feature: value.feature,
    ...(value.resource === undefined ? {} : { resource: value.resource }) };
}

function parseOpenResourceMessage(value: Record<string, unknown>): FeatureMessage | undefined {
  return bounded(value.workspace, 4096) && safeRelative(value.resource)
    && exact(value, ['type', 'workspace', 'resource'])
    ? { type: 'openFeatureResource', workspace: value.workspace, resource: value.resource }
    : undefined;
}

function parseExecuteFeatureMessage(value: Record<string, unknown>): FeatureMessage | undefined {
  const values = parseFeatureValues(value.values);
  return values !== undefined && exact(value, ['type', 'values'])
    ? { type: value.type as 'executeFeature' | 'createDraft', values }
    : undefined;
}

function hasValidFeaturePaths(value: Record<string, unknown>): boolean {
  return optionalRelative(value.directory) && optionalGitRef(value.baseReference)
    && optionalRelative(value.manifest) && optionalRelative(value.rules)
    && optionalRelative(value.baseline) && optionalRelative(value.destination)
    && optionalRelative(value.targetAdr) && optionalRelative(value.policyFile)
    && optionalGitRef(value.compareReference) && optionalStrings(value.evidence)
    && optionalStrings(value.contextFiles);
}

function hasValidFeatureText(value: Record<string, unknown>): boolean {
  return optionalBounded(value.title, 512) && optionalBounded(value.context, 20_000)
    && optionalBounded(value.model, 256) && optionalEndpoint(value.endpoint);
}

function hasValidFeatureSelections(value: Record<string, unknown>): boolean {
  return optionalChoice(value.policy, ['advisory', 'enforce'])
    && optionalChoice(value.catalog, ['standard', 'enriched'])
    && optionalChoice(value.culture, ['en-US', 'pt-BR'])
    && optionalChoice(value.template, ['configured', 'minimal', 'extended']);
}

function hasValidProviderSettings(value: Record<string, unknown>): boolean {
  if (!optionalChoice(value.provider, ['openai', 'anthropic', 'gemini', 'openai-compatible'])) return false;
  if (value.provider === 'openai-compatible') return typeof value.endpoint === 'string' && value.endpoint.length > 0;
  return value.provider === undefined || typeof value.endpoint !== 'string' || value.endpoint.length === 0;
}

function optionalStrings(value: unknown): boolean { return value === undefined || (Array.isArray(value) && value.length <= 32 && value.every((item) => safeRelative(item))); }
function optionalRelative(value: unknown): boolean { return value === undefined || safeRelative(value); }
function optionalGitRef(value: unknown): boolean { return value === undefined || (bounded(value, 256) && (value.length === 0 || isSafeGitBaseReference(value))); }
function optionalBounded(value: unknown, maximum: number): boolean { return value === undefined || bounded(value, maximum); }
function optionalBoolean(value: unknown): boolean { return value === undefined || typeof value === 'boolean'; }
function optionalChoice(value: unknown, choices: readonly string[]): boolean { return value === undefined || (typeof value === 'string' && choices.includes(value)); }
function optionalEndpoint(value: unknown): boolean { if (value === undefined || value === '') return true; if (!bounded(value, 2048)) return false; try { const uri = new URL(value); return uri.protocol === 'https:' && uri.username === '' && uri.password === ''; } catch { return false; } }
function safeRelative(value: unknown): value is string { return bounded(value, 4096) && value.trim().length > 0 && !path.isAbsolute(value) && !value.split(/[\\/]/u).includes('..'); }
function isFeature(value: unknown): value is VisualFeature { return typeof value === 'string' && features.includes(value as VisualFeature); }
function bounded(value: unknown, maximum: number): value is string { return typeof value === 'string' && value.length <= maximum && !value.includes('\0'); }
function exact(value: Record<string, unknown>, keys: readonly string[]): boolean { return Object.keys(value).every((key) => keys.includes(key)); }
function isRecord(value: unknown): value is Record<string, unknown> { return typeof value === 'object' && value !== null && !Array.isArray(value); }
