export type TemplateSelection = 'configured' | 'minimal' | 'extended' | { readonly file: string };
export type AdrFormat = 'canonical' | 'madr-4';

export interface InitArguments {
  readonly repository: string;
  readonly adrDirectory: string;
  readonly template: TemplateSelection;
  readonly githubActions: boolean;
  readonly dryRun: boolean;
}

export interface NewArguments {
  readonly adrDirectory: string;
  readonly title: string;
  readonly template: TemplateSelection;
  readonly culture: 'en-US' | 'pt-BR';
}

export interface CheckArguments {
  readonly directory: string;
  readonly adrFormat: AdrFormat;
  readonly changed?: boolean;
  readonly baseReference?: string;
  readonly baselinePath?: string;
}

export function buildInitArguments(options: InitArguments): string[] {
  return [
    'init', options.repository,
    '--adr-directory', options.adrDirectory,
    ...templateArguments(options.template),
    ...(options.githubActions ? ['--github-actions'] : []),
    ...(options.dryRun ? ['--dry-run'] : []),
  ];
}

export function buildNewArguments(options: NewArguments): string[] {
  return [
    'new', options.adrDirectory,
    '--title', options.title,
    ...templateArguments(options.template),
    '--culture', options.culture,
  ];
}

export function buildCheckArguments(options: CheckArguments): string[] {
  const changed = options.changed ?? false;
  if (changed !== (options.baseReference !== undefined)) {
    throw new Error('Changed validation requires exactly one base reference.');
  }
  const changeArguments = changed && options.baseReference !== undefined
    ? ['--changed', '--base-ref', options.baseReference]
    : [];
  return [
    'check', options.directory,
    ...adrFormatArguments(options.adrFormat),
    ...changeArguments,
    ...(options.baselinePath === undefined ? [] : ['--baseline', options.baselinePath]),
    '--format', 'json',
  ];
}

export function buildIndexArguments(directory: string, adrFormat: AdrFormat): string[] {
  return ['index', directory, ...adrFormatArguments(adrFormat)];
}

export function isSafeGitBaseReference(value: string): boolean {
  return value.length > 0
    && value.length <= 256
    && !value.startsWith('-')
    && !/[\s\p{Cc}]/u.test(value);
}

export function parseCreatedAdrPath(stdout: string): string {
  const lines = stdout.split(/\r?\n/u).filter((line) => line.length > 0);
  if (lines.length !== 1 || !lines[0]?.startsWith('ADR written: ')) {
    throw new Error('The CLI reported an unsafe or unexpected created file path.');
  }
  const reported = lines[0].slice('ADR written: '.length);
  if (reported.trim() !== reported || reported.length === 0 || reported.includes('\0')) {
    throw new Error('The CLI reported an unsafe or unexpected created file path.');
  }
  return reported;
}

function templateArguments(template: TemplateSelection): string[] {
  if (template === 'configured') {
    return [];
  }
  if (typeof template === 'string') {
    return ['--template', template];
  }
  return ['--template-file', template.file];
}

function adrFormatArguments(format: AdrFormat): string[] {
  return format === 'canonical' ? [] : ['--adr-format', format];
}
