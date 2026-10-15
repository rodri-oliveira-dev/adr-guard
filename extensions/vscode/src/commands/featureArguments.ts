import type { AiProvider } from '../webview/featureMessages';

export function impactArguments(repository: string, baseReference: string, manifest: string): string[] {
  return ['impact', repository, '--base-ref', baseReference, '--map', manifest, '--format', 'json'];
}

export function driftArguments(repository: string, rules: string, evidence: readonly string[], baseline: string | undefined, policy: 'advisory' | 'enforce'): string[] {
  return ['drift', repository, '--rules', rules, ...evidence.flatMap((file) => ['--evidence', file]), ...(baseline ? ['--baseline', baseline] : []), '--format', 'json', '--policy', policy];
}

export function baselineArguments(directory: string, destination: string, update: boolean): string[] {
  return ['baseline', directory, '--output', destination, ...(update ? ['--update'] : [])];
}

export function catalogArguments(directory: string, catalog: 'standard' | 'enriched'): string[] {
  return ['index', directory, ...(catalog === 'enriched' ? ['--catalog', 'enriched'] : [])];
}

interface ProviderArguments {
  readonly provider: AiProvider;
  readonly model: string;
  readonly endpoint?: string;
  readonly contextFiles: readonly string[];
  readonly includeExistingAdrs: boolean;
}

export function draftArguments(directory: string, title: string, context: string, culture: 'en-US' | 'pt-BR', template: 'configured' | 'minimal' | 'extended', provider: ProviderArguments, preview: boolean): string[] {
  return ['draft', directory, '--title', title, '--context', context, '--provider', provider.provider, '--model', provider.model, '--culture', culture,
    ...(template === 'configured' ? [] : ['--template', template]), ...(provider.endpoint ? ['--endpoint', provider.endpoint] : []),
    ...provider.contextFiles.flatMap((file) => ['--context-file', file]), ...(provider.includeExistingAdrs ? ['--include-existing-adrs'] : []), ...(preview ? ['--preview'] : [])];
}

export function reviewArguments(target: string, policy: 'advisory' | 'enforce', policyFile: string | undefined, compareReference: string | undefined, provider: ProviderArguments): string[] {
  return ['review', target, '--provider', provider.provider, '--model', provider.model, '--format', 'json', '--policy', policy,
    ...(provider.endpoint ? ['--endpoint', provider.endpoint] : []), ...(compareReference ? ['--compare-ref', compareReference] : []),
    ...(policyFile ? ['--policy-file', policyFile] : []), ...provider.contextFiles.flatMap((file) => ['--context-file', file]),
    ...(provider.includeExistingAdrs ? ['--include-existing-adrs'] : [])];
}
