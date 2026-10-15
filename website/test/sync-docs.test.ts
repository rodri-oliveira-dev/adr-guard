import { readFile } from 'node:fs/promises';
import { describe, expect, it } from 'vitest';

const generatedRoot = new URL('../src/content/docs/', import.meta.url);

describe('synchronized documentation', () => {
  it('localizes source badges from the destination locale', async () => {
    const [english, portuguese] = await Promise.all([
      readFile(new URL('product/vscode-extension.md', generatedRoot), 'utf8'),
      readFile(new URL('pt-br/product/vscode-extension.md', generatedRoot), 'utf8'),
    ]);

    expect(english).toContain('text: Source synced');
    expect(english).not.toContain('text: Fonte sincronizada');
    expect(portuguese).toContain('text: Fonte sincronizada');
    expect(portuguese).not.toContain('text: Source synced');
  });

  it('rewrites Marketplace image URLs to the GitHub Pages base path', async () => {
    const [english, portuguese] = await Promise.all([
      readFile(new URL('product/vscode-extension.md', generatedRoot), 'utf8'),
      readFile(new URL('pt-br/product/vscode-extension.md', generatedRoot), 'utf8'),
    ]);

    for (const markdown of [english, portuguese]) {
      expect(markdown).toContain('](/adr-guard/images/vscode/adr-list.png)');
      expect(markdown).not.toContain('raw.githubusercontent.com');
    }
  });
});
