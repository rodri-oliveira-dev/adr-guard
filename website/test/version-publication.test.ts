import { readFile } from 'node:fs/promises';
import { describe, expect, it } from 'vitest';

const documentationRoot = new URL('../../docs/', import.meta.url);
const portalRoot = new URL('../src/content/docs/', import.meta.url);
const extensionRoot = new URL('../../extensions/vscode/', import.meta.url);

function publishedVersion(matrix: string, rowPrefix: string, statusPattern: RegExp): string {
  const row = matrix.split(/\r?\n/).find((line) => line.startsWith(rowPrefix));
  if (!row) throw new Error(`Missing compatibility row: ${rowPrefix}`);
  const version = row.match(statusPattern)?.[1];
  if (!version) throw new Error(`Missing published version in compatibility row: ${rowPrefix}`);
  return version;
}

describe('published product version documentation', () => {
  it('keeps CLI, Action and portal documentation aligned with the published CLI version', async () => {
    const [matrix, matrixPt, cli, cliPt, action, actionPt, portal, portalPt] =
      await Promise.all([
        readFile(new URL('compatibility.md', documentationRoot), 'utf8'),
        readFile(new URL('compatibility.pt-BR.md', documentationRoot), 'utf8'),
        readFile(new URL('cli-reference.md', documentationRoot), 'utf8'),
        readFile(new URL('cli-reference.pt-BR.md', documentationRoot), 'utf8'),
        readFile(new URL('github-action.md', documentationRoot), 'utf8'),
        readFile(new URL('github-action.pt-BR.md', documentationRoot), 'utf8'),
        readFile(new URL('product/index.md', portalRoot), 'utf8'),
        readFile(new URL('pt-br/product/index.md', portalRoot), 'utf8'),
      ]);

    const version = publishedVersion(matrix, '| CLI / .NET Tool |', /\*\*Published:\*\* `(\d+\.\d+\.\d+)`/);
    expect(matrixPt).toContain(`**Publicado:** \`${version}\` no NuGet.org`);
    expect(cli).toContain(`CLI \`${version}\` is published on`);
    expect(cliPt).toContain(`A CLI \`${version}\` está publicada no`);
    expect(action).toContain(`@v${version}`);
    expect(actionPt).toContain(`@v${version}`);
    expect(portal).toContain(`CLI ${version} is published`);
    expect(portalPt).toContain(`A CLI ${version} está publicada`);
  });

  it('keeps Marketplace and MCP instructions aligned with the published extension version', async () => {
    const [matrix, matrixPt, extension, extensionPt, portal, portalPt] =
      await Promise.all([
        readFile(new URL('compatibility.md', documentationRoot), 'utf8'),
        readFile(new URL('compatibility.pt-BR.md', documentationRoot), 'utf8'),
        readFile(new URL('README.md', extensionRoot), 'utf8'),
        readFile(new URL('README.pt-BR.md', extensionRoot), 'utf8'),
        readFile(new URL('product/index.md', portalRoot), 'utf8'),
        readFile(new URL('pt-br/product/index.md', portalRoot), 'utf8'),
      ]);

    const version = publishedVersion(
      matrix,
      '| VS Code core commands, Problems, Explorer |',
      /\*\*Published:\*\* Marketplace `(\d+\.\d+\.\d+)`/,
    );
    expect(matrixPt).toContain(`**Publicado:** Marketplace \`${version}\``);
    expect(extension).toContain(`extension \`${version}\` is published`);
    expect(extensionPt).toContain(`a extensão \`${version}\` está publicada`);
    expect(portal).toContain(`VS Code extension ${version}`);
    expect(portalPt).toContain(`extensão VS Code ${version}`);
    const manifest = JSON.parse(
      await readFile(new URL('../../.mcp/server.json', import.meta.url), 'utf8'),
    ) as { version: string; packages: { version: string }[] };
    expect(manifest.packages[0]?.version).toBe(manifest.version);
    expect(extension).toContain(`RodriOliveira.AdrGuard.Mcp --version ${manifest.version}`);
    expect(extensionPt).toContain(`RodriOliveira.AdrGuard.Mcp --version ${manifest.version}`);
  });
});
