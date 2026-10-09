import { readFile, readdir, stat } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';

const dist = resolve(import.meta.dirname, '../dist');
const htmlFiles = [];
async function walk(directory) {
  for (const name of await readdir(directory)) {
    const path = resolve(directory, name);
    (await stat(path)).isDirectory() ? await walk(path) : name.endsWith('.html') && htmlFiles.push(path);
  }
}
await walk(dist);

const failures = [];
for (const file of htmlFiles) {
  const html = await readFile(file, 'utf8');
  for (const match of html.matchAll(/(?:href|src)="([^"]+)"/g)) {
    const value = match[1];
    if (!value || /^(?:https?:|mailto:|data:|#)/.test(value)) continue;
    const clean = value.split(/[?#]/)[0];
    let target;
    if (clean.startsWith('/adr-guard/')) target = resolve(dist, clean.slice('/adr-guard/'.length));
    else if (clean.startsWith('/')) continue;
    else target = resolve(dirname(file), clean);
    try {
      const info = await stat(target);
      if (info.isDirectory()) await stat(resolve(target, 'index.html'));
    } catch { failures.push(`${file}: ${value}`); }
  }
}
if (failures.length) throw new Error(`Broken local links:\n${failures.slice(0, 30).join('\n')}`);
console.log(`Checked local links in ${htmlFiles.length} HTML files.`);
