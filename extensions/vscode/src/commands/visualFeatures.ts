import * as vscode from 'vscode';
import { CliExecutor, requireSuccessfulResult, safeErrorLine } from '../cli/executor';
import { parseDriftReport, parseImpactReport, parseReviewReport, type DriftReport, type ImpactReport, type ReviewReport } from '../contracts/architecture';
import { asLocation } from '../workspace';
import { resolveWorkspaceRoot } from '../workspacePolicy';
import type { FeatureValues, VisualFeature } from '../webview/featureMessages';
import { featureExecutionArguments } from './featureExecutionArguments';

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
    return featureExecutionArguments(values, root, createDraft, (label) =>
      new Error(vscode.l10n.t('Enter a valid {0}.', label)));
  }
}

function capabilityFlag(feature: VisualFeature, args: readonly string[]): string {
  if (feature === 'catalog') return args.includes('--catalog') ? '--catalog enriched' : 'adr-guard index';
  if (feature === 'draft') return '--preview';
  if (feature === 'review' && args.includes('--compare-ref')) return '--compare-ref';
  return `adr-guard ${feature}`;
}
export function extractDraftMarkdown(output: string): string {
  const marker = /ADR draft preview path:[^\r\n]*\r?\n\r?\n/u.exec(output);
  if (marker?.index === undefined) throw new Error(vscode.l10n.t('ADR Guard returned an invalid draft preview.'));
  const markdown = output.slice(marker.index + marker[0].length);
  if (!markdown.trimStart().startsWith('#')) throw new Error(vscode.l10n.t('ADR Guard returned an invalid draft preview.'));
  return markdown;
}
