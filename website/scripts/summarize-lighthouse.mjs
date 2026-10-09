import { appendFileSync, existsSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
const config = require('../lighthouserc.cjs');
const assertions = config.ci.assert.assertions;
const directory = '.lighthouseci';
const categories = [
  ['performance', 'Performance'],
  ['accessibility', 'Accessibility'],
  ['best-practices', 'Best Practices'],
  ['seo', 'SEO']
];

if (!existsSync(directory)) throw new Error('Lighthouse report directory was not created.');
const reports = readdirSync(directory)
  .filter((file) => file.endsWith('.json'))
  .map((file) => JSON.parse(readFileSync(join(directory, file), 'utf8')))
  .filter((report) => report.categories && report.finalUrl);
if (!reports.length) throw new Error('No Lighthouse JSON reports were found.');

const median = (values) => [...values].sort((a, b) => a - b)[Math.floor(values.length / 2)];
const percent = (value) => `${Math.round(value * 100)}%`;
const byUrl = new Map();
for (const report of reports) {
  const page = byUrl.get(report.finalUrl) ?? Object.fromEntries(categories.map(([id]) => [id, []]));
  for (const [id] of categories) page[id].push(report.categories[id].score);
  byUrl.set(report.finalUrl, page);
}

const lines = [
  '# Lighthouse CI', '',
  'Scores are the median of three runs per representative page.', '',
  '| URL | Performance | Accessibility | Best Practices | SEO |',
  '| --- | ---: | ---: | ---: | ---: |'
];
const scores = new Map();
for (const [url, samples] of byUrl) {
  const result = Object.fromEntries(categories.map(([id]) => [id, median(samples[id])]));
  scores.set(url, result);
  lines.push(`| \`${new URL(url).pathname}\` | ${percent(result.performance)} | ${percent(result.accessibility)} | ${percent(result['best-practices'])} | ${percent(result.seo)} |`);
}

lines.push('', '## Quality gates', '', '| Category | Threshold | Enforcement | Lowest median | Result |', '| --- | ---: | --- | ---: | --- |');
for (const [id, label] of categories) {
  const [level, options] = assertions[`categories:${id}`];
  const actual = Math.min(...[...scores.values()].map((entry) => entry[id]));
  const passed = actual >= options.minScore;
  lines.push(`| ${label} | ${percent(options.minScore)} | ${level === 'error' ? 'Blocking' : 'Warning'} | ${percent(actual)} | ${passed ? 'PASS' : level === 'error' ? 'FAIL' : 'WARN'} |`);
}

const report = `${lines.join('\n')}\n`;
writeFileSync(join(directory, 'summary.md'), report, 'utf8');
if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY, report, 'utf8');
console.log(report);
