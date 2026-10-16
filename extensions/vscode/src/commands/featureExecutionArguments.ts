import path from 'node:path';
import type { FeatureValues } from '../webview/featureMessages';
import { ensurePotentialFileWithinWorkspace, ensureResourceWithinWorkspace, resolveWorkspaceRelativePath } from '../workspacePolicy';
import { baselineArguments, catalogArguments, draftArguments, driftArguments, impactArguments, reviewArguments } from './featureArguments';

type MissingValueError = (label: string) => Error;

export async function featureExecutionArguments(
  values: FeatureValues,
  root: string,
  createDraft: boolean,
  missingValueError: MissingValueError,
): Promise<string[]> {
  const required = <T extends string>(value: T | undefined, label: string): T => {
    if (value === undefined || value.trim().length === 0) throw missingValueError(label);
    return value;
  };

  switch (values.feature) {
    case 'impact':
      return impactArguments(root, required(values.baseReference, 'base reference'), await existingRepositoryRelativeFile(root, required(values.manifest, 'impact manifest')));
    case 'drift': {
      const evidence: string[] = [];
      for (const item of values.evidence ?? []) evidence.push(await existingRepositoryRelativeFile(root, item));
      return driftArguments(root, await existingRepositoryRelativeFile(root, required(values.rules, 'fitness rules')), evidence,
        values.baseline ? await existingRepositoryRelativeFile(root, values.baseline) : undefined, values.policy ?? 'advisory');
    }
    case 'baseline': {
      const directory = await existingDirectory(root, values.directory ?? 'docs/adr');
      return baselineArguments(directory, await potentialFile(root, required(values.destination, 'baseline destination')), values.update === true);
    }
    case 'catalog': {
      const directory = await existingDirectory(root, values.directory ?? 'docs/adr');
      return catalogArguments(directory, values.catalog ?? 'standard');
    }
    case 'draft': {
      const directory = await existingDirectory(root, values.directory ?? 'docs/adr');
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

async function existingDirectory(root: string, relative: string): Promise<string> {
  return ensureResourceWithinWorkspace(resolveWorkspaceRelativePath(root, relative), root);
}

async function existingFile(root: string, relative: string): Promise<string> {
  return ensureResourceWithinWorkspace(resolveWorkspaceRelativePath(root, relative), root);
}

async function existingRepositoryRelativeFile(root: string, relative: string): Promise<string> {
  const [resolved, resolvedRoot] = await Promise.all([
    existingFile(root, relative),
    ensureResourceWithinWorkspace(root, root),
  ]);
  return path.relative(resolvedRoot, resolved).split(path.sep).join('/');
}

async function potentialFile(root: string, relative: string): Promise<string> {
  return ensurePotentialFileWithinWorkspace(resolveWorkspaceRelativePath(root, relative), root);
}
