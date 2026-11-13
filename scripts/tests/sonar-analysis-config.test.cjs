'use strict';

const {test} = require('node:test');
const assert = require('node:assert/strict');
const {readFileSync} = require('node:fs');
const {resolve} = require('node:path');

test('Sonar excludes generated coverage artifacts while importing LCOV data', () => {
  const workflow = readFileSync(
    resolve(__dirname, '../../.github/workflows/sonar.yml'),
    'utf8'
  );
  const exclusions = workflow.match(/\/d:sonar\.exclusions="([^"]+)"/);
  assert.ok(exclusions, 'Sonar source exclusion setting must exist');
  assert.ok(
    exclusions[1].split(',').includes('artifacts/**'),
    'generated artifacts must not be analyzed as source'
  );
  assert.match(
    workflow,
    /\/d:sonar\.javascript\.lcov\.reportPaths="artifacts\/coverage\/sonar\/vscode\/lcov\.info"/,
    'LCOV coverage import must remain available'
  );
});

test('Sonar coverage includes every production .NET assembly', () => {
  const settings = readFileSync(
    resolve(__dirname, '../../eng/sonar-coverage.settings.xml'),
    'utf8'
  );
  const workflow = readFileSync(
    resolve(__dirname, '../../.github/workflows/sonar.yml'),
    'utf8'
  );

  for (const assembly of [
    'AdrGuard\\.Core',
    'adr-guard',
    'adr-guard-agent',
    'adr-guard-mcp'
  ]) {
    assert.ok(
      settings.includes(assembly),
      `coverage settings must include ${assembly}`
    );
    assert.ok(
      workflow.includes(assembly.replace('\\.', '.')),
      `coverage verification must require ${assembly}`
    );
  }
});
