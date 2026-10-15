import { readFile } from 'node:fs/promises';
import path from 'node:path';

const extensionRoot = path.resolve(import.meta.dirname, '..');
const repositoryRoot = path.resolve(extensionRoot, '..', '..');
const readmes = ['README.md', 'README.pt-BR.md'];
const rawPrefix = 'https://raw.githubusercontent.com/rodri-oliveira-dev/adr-guard/main/';
const pngSignature = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
const checkRemote = process.argv.includes('--remote');

function assertPng(buffer, source) {
  if (buffer.length < 24 || !buffer.subarray(0, pngSignature.length).equals(pngSignature)) {
    throw new Error(`${source} is not a valid PNG image`);
  }
  if (buffer.toString('ascii', 12, 16) !== 'IHDR') {
    throw new Error(`${source} does not contain a PNG IHDR header`);
  }
  if (buffer.readUInt32BE(16) === 0 || buffer.readUInt32BE(20) === 0) {
    throw new Error(`${source} has invalid PNG dimensions`);
  }
}

const readmeImages = new Map();
for (const readme of readmes) {
  const markdown = await readFile(path.join(extensionRoot, readme), 'utf8');
  const images = [...markdown.matchAll(/!\[([^\]]+)\]\(([^)]+)\)/g)];
  if (images.length === 0) throw new Error(`${readme} does not contain any image references`);

  for (const [, alt, url] of images) {
    if (!alt.trim()) throw new Error(`${readme} contains an image without alternative text`);
    if (!url.startsWith(rawPrefix)) {
      throw new Error(`${readme} image must use the permanent raw main URL: ${url}`);
    }
    const repositoryPath = url.slice(rawPrefix.length);
    if (!/^website\/public\/images\/vscode\/[a-z0-9-]+\.png$/.test(repositoryPath)) {
      throw new Error(`${readme} image points outside the VS Code image directory: ${url}`);
    }
    const localPath = path.resolve(repositoryRoot, ...repositoryPath.split('/'));
    const localImage = await readFile(localPath);
    assertPng(localImage, repositoryPath);
    readmeImages.set(url, localImage);
  }
}

if (checkRemote) {
  for (const [url] of readmeImages) {
    const response = await fetch(url, { signal: AbortSignal.timeout(15_000) });
    if (!response.ok) throw new Error(`${url} returned HTTP ${response.status}`);
    const contentType = response.headers.get('content-type')?.split(';', 1)[0].trim().toLowerCase();
    if (contentType !== 'image/png') throw new Error(`${url} returned ${contentType ?? 'no content type'}`);
    assertPng(Buffer.from(await response.arrayBuffer()), url);
  }
}

console.log(`Validated ${readmeImages.size} Marketplace image resources${checkRemote ? ' locally and over HTTPS' : ''}.`);
