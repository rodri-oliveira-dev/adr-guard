'use strict';

const {test} = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const updateQualityGate = require('../sonar-quality-gate.cjs');

test('excludes repository automation from coverage but retains static analysis', () => {
  const workflow = fs.readFileSync(
    path.resolve(__dirname, '../../.github/workflows/sonar.yml'),
    'utf8',
  );
  const sourceExclusions = workflow.match(/\/d:sonar\.exclusions="([^"]+)"/)?.[1] ?? '';
  const coverageExclusions = workflow.match(/\/d:sonar\.coverage\.exclusions="([^"]+)"/)?.[1] ?? '';

  assert.match(coverageExclusions, /(?:^|,)eng\/\*\*(?:,|$)/);
  assert.doesNotMatch(sourceExclusions, /(?:^|,)eng\/\*\*(?:,|$)/);
});

function createFake(existingBody = '') {
  const created = [];
  const updated = [];
  const failures = [];
  const comments = existingBody
    ? [{id: 45, body: existingBody, user: {login: 'github-actions[bot]'}}]
    : [];
  const github = {
    rest: {
      issues: {
        listComments: async () => ({data: []}),
        createComment: async (args) => created.push(args),
        updateComment: async (args) => updated.push(args),
      },
    },
    paginate: async () => comments,
  };
  const core = {
    setSecret() {},
    setFailed(message) { failures.push(message); },
  };
  return {github, core, created, updated, failures};
}

async function exercise(fetchImpl, scanResult = 'success', existingBody = '') {
  const fake = createFake(existingBody);
  const original = {
    GITHUB_REPOSITORY: process.env.GITHUB_REPOSITORY,
    GITHUB_RUN_ID: process.env.GITHUB_RUN_ID,
    GITHUB_SERVER_URL: process.env.GITHUB_SERVER_URL,
    SONAR_PROJECT_KEY: process.env.SONAR_PROJECT_KEY,
    SONAR_SCAN_RESULT: process.env.SONAR_SCAN_RESULT,
    SONAR_TOKEN: process.env.SONAR_TOKEN,
  };
  Object.assign(process.env, {
    GITHUB_REPOSITORY: 'team/adr-guard',
    GITHUB_RUN_ID: '123',
    GITHUB_SERVER_URL: 'https://github.com',
    SONAR_PROJECT_KEY: 'test_project',
    SONAR_SCAN_RESULT: scanResult,
    SONAR_TOKEN: 'fake-token',
  });

  try {
    await updateQualityGate({
      github: fake.github,
      context: {repo: {owner: 'team', repo: 'adr-guard'}, issue: {number: 211}},
      core: fake.core,
      fetchImpl,
      readTaskId: () => 'task-current',
    });
    return fake;
  } finally {
    for (const [key, value] of Object.entries(original)) {
      if (value === undefined) delete process.env[key];
      else process.env[key] = value;
    }
  }
}

test('reports the current failed Quality Gate when the scanner outcome is failure', {concurrency: false}, async () => {
  const requestedUrls = [];
  const fetchImpl = async (url) => {
    requestedUrls.push(url.toString());
    if (url.pathname === '/api/ce/task') {
      return {
        ok: true,
        status: 200,
        json: async () => ({task: {id: 'task-current', status: 'SUCCESS', analysisId: 'analysis-current'}}),
      };
    }
    return {
      ok: true,
      status: 200,
      json: async () => ({
        projectStatus: {
          status: 'ERROR',
          conditions: [{metricKey: 'coverage', status: 'ERROR', actualValue: '72', errorThreshold: '80'}],
        },
      }),
    };
  };

  const fake = await exercise(fetchImpl, 'failure');

  assert.equal(fake.created.length, 1);
  assert.match(fake.created[0].body, /\*\*Status: ERROR\*\*/);
  assert.match(fake.created[0].body, /`coverage` \| ERROR \| 72 \| 80/);
  assert.deepEqual(fake.failures, ['Sonar Quality Gate status ERROR.']);
  assert.match(requestedUrls[0], /api\/ce\/task\?id=task-current/);
  assert.match(requestedUrls[1], /api\/qualitygates\/project_status\?analysisId=analysis-current/);
  assert.doesNotMatch(requestedUrls[1], /projectKey|pullRequest/);
});

for (const [name, fetchImpl] of [
  ['network failure', async () => { throw new Error('provider detail'); }],
  ['invalid JSON', async () => ({ok: true, status: 200, json: async () => { throw new SyntaxError('provider detail'); }})],
]) {
  test(`refreshes the comment as UNKNOWN after ${name}`, {concurrency: false}, async () => {
    const staleBody = '<!-- adr-guard:sonar-quality-gate -->\n**Status: OK**';
    const fake = await exercise(fetchImpl, 'success', staleBody);
    const refreshed = fake.updated[0];

    assert.equal(fake.created.length, 0);
    assert.equal(fake.updated.length, 1);
    assert.match(refreshed.body, /\*\*Status: UNKNOWN\*\*/);
    assert.match(refreshed.body, /lookup failed for the current analysis/);
    assert.doesNotMatch(refreshed.body, /provider detail/);
    assert.deepEqual(fake.failures, ['Sonar Quality Gate status UNKNOWN.']);
  });
}
