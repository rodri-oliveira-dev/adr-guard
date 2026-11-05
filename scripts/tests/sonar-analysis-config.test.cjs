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
