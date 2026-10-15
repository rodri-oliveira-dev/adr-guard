import * as vscode from 'vscode';
import { CliExecutor, requireSuccessfulResult, safeErrorLine } from '../cli/executor';
import { parseDriftReport, parseImpactReport, parseReviewReport, type DriftReport, type ImpactReport, type ReviewReport } from '../contracts/architecture';
import { asLocation } from '../workspace';
import { ensurePotentialFileWithinWorkspace, ensureResourceWithinWorkspace, resolveWorkspaceRelativePath, resolveWorkspaceRoot } from '../workspacePolicy';
import type { FeatureValues, VisualFeature } from '../webview/featureMessages';
import { baselineArguments, catalogArguments, draftArguments, driftArguments, impactArguments, reviewArguments } from './featureArguments';

export type FeatureResult =
  | { readonly feature: 'impact'; readonly report: ImpactReport }
  | { readonly feature: 'drift'; readonly report: DriftReport; readonly exitCode: number; readonly diagnostic?: string }
  | { readonly feature: 'review'; readonly report: ReviewReport; readonly compareReference?: string }
  | { readonly feature: 'draft'; readonly markdown?: string; readonly output: string; readonly created: boolean }
  | { readonly feature: 'baseline' | 'catalog'; readonly output: string };

export class VisualFeatureExecutor {
  public constructor(private readonly executor: CliExecutor) {}

  public async run(folder: vscode.WorkspaceFolder, values: FeatureValues, signal: AbortSignal, createDraft = false): Promise<FeatureResult> {
    const root = await resolveWorkspaceRoot(asLocation(folder));
    const args = await this.arguments(values, root, createDraft);
    await this.requireCapability(folder, values.feature, args, signal);
    const execution = await this.executor.execute(folder, args, signal);
    const result = execution.result;
    if (values.feature === 'drift' && result.termination === 'exited' && (result.exitCode === 0 || result.exitCode === 3 || result.exitCode === 4)) {
      const diagnostic = safeErrorLine(result.stderr);
      return { feature: 'drift', report: parseDriftReport(result.stdout.trim()), exitCode: result.exitCode, ...(diagnostic === undefined ? {} : { diagnostic }) };
    }
    requireSuccessfulResult(result);
    if (values.feature === 'impact') return { feature: 'impact', report: parseImpactReport(result.stdout.trim()) };
    if (values.feature === 'review') return { feature: 'review', report: parseReviewReport(result.stdout.trim()), ...(values.compareReference ? { compareReference: values.compareReference } : {}) };
    if (values.feature === 'draft') {
      return createDraft
        ? { feature: 'draft', output: result.stdout.trim(), created: true }
        : { feature: 'draft', output: result.stdout.trim(), markdown: extractDraftMarkdown(result.stdout), created: false };
    }
    if (values.feature === 'baseline' || values.feature === 'catalog') return { feature: values.feature, output: result.stdout.trim() };
    throw new Error(vscode.l10n.t('ADR Guard returned an invalid feature result.'));
  }

  private async requireCapability(folder: vscode.WorkspaceFolder, feature: VisualFeature, args: readonly string[], signal: AbortSignal): Promise<void> {
    const help = await this.executor.execute(folder, [feature === 'catalog' ? 'index' : feature, '--help'], signal);
    requireSuccessfulResult(help.result);
    const required = capabilityFlag(feature, args);
    if (!help.result.stdout.includes(required)) {
      throw new Error(vscode.l10n.t('The installed ADR Guard CLI does not support {0}. Update the CLI before using this feature.', required));
    }
  }

  private async arguments(values: FeatureValues, root: string, createDraft: boolean): Promise<string[]> {
    const directory = await existingDirectory(root, values.directory ?? 'docs/adr');
    switch (values.feature) {
      case 'impact':
        return impactArguments(root, required(values.baseReference, 'base reference'), await existingFile(root, required(values.manifest, 'impact manifest')));
      case 'drift': {
        const evidence: string[] = [];
        for (const item of values.evidence ?? []) evidence.push(await existingFile(root, item));
        return driftArguments(root, await existingFile(root, required(values.rules, 'fitness rules')), evidence,
          values.baseline ? await existingFile(root, values.baseline) : undefined, values.policy ?? 'advisory');
      }
      case 'baseline':
        return baselineArguments(directory, await potentialFile(root, required(values.destination, 'baseline destination')), values.update === true);
      case 'catalog':
        return catalogArguments(directory, values.catalog ?? 'standard');
      case 'draft': {
        const contextFiles: string[] = [];
        for (const item of values.contextFiles ?? []) contextFiles.push(await existingFile(root, item));
        return draftArguments(directory, required(values.title, 'title'), required(values.context, 'context'), values.culture ?? 'en-US', values.template ?? 'configured', {
          provider: required(values.provider, 'provider'), model: required(values.model, 'model'), ...(values.endpoint ? { endpoint: values.endpoint } : {}), contextFiles, includeExistingAdrs: values.includeExistingAdrs === true,
        }, !createDraft);
      }
      case 'review': {
        const contextFiles: string[] = [];
        for (const item of values.contextFiles ?? []) contextFiles.push(await existingFile(root, item));
        return reviewArguments(await existingFile(root, required(values.targetAdr, 'ADR')), values.policy ?? 'advisory',
          values.policyFile ? await existingFile(root, values.policyFile) : undefined, values.compareReference, {
            provider: required(values.provider, 'provider'), model: required(values.model, 'model'), ...(values.endpoint ? { endpoint: values.endpoint } : {}), contextFiles, includeExistingAdrs: values.includeExistingAdrs === true,
          });
      }
    }
  }
}

function capabilityFlag(feature: VisualFeature, args: readonly string[]): string {
  if (feature === 'catalog') return args.includes('--catalog') ? '--catalog enriched' : 'adr-guard index';
  if (feature === 'draft') return '--preview';
  if (feature === 'review' && args.includes('--compare-ref')) return '--compare-ref';
  return `adr-guard ${feature}`;
}
function required<T extends string>(value: T | undefined, label: string): T { if (value === undefined || value.trim().length === 0) throw new Error(vscode.l10n.t('Enter a valid {0}.', label)); return value; }
async function existingDirectory(root: string, relative: string): Promise<string> { return ensureResourceWithinWorkspace(resolveWorkspaceRelativePath(root, relative), root); }
async function existingFile(root: string, relative: string): Promise<string> { return ensureResourceWithinWorkspace(resolveWorkspaceRelativePath(root, relative), root); }
async function potentialFile(root: string, relative: string): Promise<string> { return ensurePotentialFileWithinWorkspace(resolveWorkspaceRelativePath(root, relative), root); }
export function extractDraftMarkdown(output: string): string {
  const marker = /ADR draft preview path:[^\r\n]*\r?\n\r?\n/u.exec(output);
  if (marker?.index === undefined) throw new Error(vscode.l10n.t('ADR Guard returned an invalid draft preview.'));
  const markdown = output.slice(marker.index + marker[0].length);
  if (!markdown.trimStart().startsWith('#')) throw new Error(vscode.l10n.t('ADR Guard returned an invalid draft preview.'));
  return markdown;
}
