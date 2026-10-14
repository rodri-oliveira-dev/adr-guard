export type VisualEditFormat = 'canonical' | 'madr-4';

export function buildVisualEditArguments(
  filePath: string,
  directory: string,
  format: VisualEditFormat,
  helpText: string,
): string[] {
  return [
    'edit', filePath, '--form',
    ...(supportsAdrDirectory(helpText) ? ['--adr-directory', directory] : []),
    '--adr-format', format,
  ];
}

export function supportsAdrDirectory(helpText: string): boolean {
  return /(?:^|[^A-Za-z0-9-])--adr-directory(?=\s|\])/u.test(helpText);
}
