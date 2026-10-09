import { access, readdir } from 'node:fs/promises';
import { resolve } from 'node:path';

const root = resolve(import.meta.dirname, '../dist');
const required = [
  'index.html', 'getting-started/index.html', 'templates/index.html', 'adoption/index.html', 'examples/index.html',
  'examples/redis-cache/index.html', 'examples/service-communication/index.html', 'examples/database-selection/index.html',
  'examples/authentication/index.html', 'examples/supersession/index.html', 'product/index.html', 'learn/what-is-an-adr/index.html'
];

for (const route of required) {
  await access(resolve(root, route));
  await access(resolve(root, 'pt-br', route));
}

const pagefindFiles = await readdir(resolve(root, 'pagefind'));
if (!pagefindFiles.some((name) => name.startsWith('pagefind.'))) throw new Error('Pagefind output was not generated.');
console.log(`Verified ${required.length * 2} bilingual routes and Pagefind output.`);
