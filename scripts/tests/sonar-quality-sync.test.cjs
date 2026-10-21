'use strict';

const { test } = require('node:test');
const assert = require('node:assert/strict');
const sync = require('../sonar-quality-sync.cjs');

function createFake() {
  const issues = [];
  const created = [];
  const updated = [];
  const github = {
    rest: {
      repos: { get: async () => ({data: {default_branch: 'main'}}) },
      issues: {
        getLabel: async () => ({data: {name: 'Sonar Quality Issues'}}),
        createLabel: async () => ({data: {}}),
        listForRepo: async () => ({data: issues}),
        create: async (args) => {
          const issue = {number: issues.length + 1, title: args.title, body: args.body, labels: args.labels, state: 'open', user: {login: 'github-actions[bot]'}};
          issues.push(issue);
          created.push(args);
          return {data: issue};
        },
        update: async (args) => {
          const issue = issues.find(x => x.number === args.issue_number);
          Object.assign(issue, args);
          updated.push(args);
          return {data: issue};
        },
        addLabels: async (args) => {
          const issue = issues.find(x => x.number === args.issue_number);
          issue.labels = args.labels;
          return {data: issue};
        }
      }
    },
    paginate: async () => [...issues]
  };
  const summary = {addHeading() {return this;}, addTable() {return this;}, async write() {}};
  const core = {setSecret() {}, info() {}, warning() {}, summary};
  return {github, core, issues, created, updated};
}

async function exercise(apiIssues, fake, ok = true) {
  const originalFetch = global.fetch;
  const env = {SONAR_HOST_URL: process.env.SONAR_HOST_URL, SONAR_PROJECT_KEY: process.env.SONAR_PROJECT_KEY, SONAR_ISSUE_LABEL: process.env.SONAR_ISSUE_LABEL, SONAR_TOKEN: process.env.SONAR_TOKEN};
  process.env.SONAR_HOST_URL = 'https://sonarcloud.io';
  process.env.SONAR_PROJECT_KEY = 'test_project';
  process.env.SONAR_ISSUE_LABEL = 'Sonar Quality Issues';
  process.env.SONAR_TOKEN = 'fake-token';
  global.fetch = async () => ({
    ok, status: ok ? 200 : 503,
    json: async () => ({issues: apiIssues, rules: [], paging: {total: apiIssues.length}}),
    text: async () => 'Unavailable'
  });
  try {
    await sync({github: fake.github, core: fake.core, context: {repo: {owner: 'team', repo: 'adr-guard'}}});
  } finally {
    global.fetch = originalFetch;
    for (const [key, value] of Object.entries(env)) {
      if (value === undefined) delete process.env[key];
      else process.env[key] = value;
    }
  }
}

const finding = {
  key: 'SONAR-A',
  component: 'test_project:src/AdrGuard.Core/Validation/AdrValidator.cs',
  impacts: [{severity: 'HIGH', softwareQuality: 'MAINTAINABILITY'}],
  rule: 'csharpsquid:S1118',
  message: 'Review the implementation',
  tags: ['maintainability']
};

test('creates a high-impact issue with stable provenance, without closing it', {concurrency: false}, async () => {
  const fake = createFake();
  await exercise([finding], fake);
  assert.equal(fake.created.length, 1);
  assert.match(fake.issues[0].body, /sonar-issue-key:SONAR-A/);
  assert.match(fake.issues[0].body, /sonar-sync:start/);
  assert.equal(fake.issues[0].state, 'open');
});

test('repeat runs are idempotent and preserve manual notes', {concurrency: false}, async () => {
  const fake = createFake();
  await exercise([finding], fake);
  fake.issues[0].body += '\n## Human notes\nInvestigating in a follow-up.';
  await exercise([finding], fake);
  assert.equal(fake.created.length, 1);
  assert.equal(fake.updated.length, 0);
  assert.match(fake.issues[0].body, /Investigating in a follow-up/);
  await exercise([], fake);
  assert.equal(fake.issues[0].state, 'open');
});

test('ignores user-authored lookalike issues', {concurrency: false}, async () => {
  const fake = createFake();
  fake.issues.push({
    number: 45, state: 'open', title: 'Manual issue', body: '<!-- sonar-sync:start --><!-- sonar-issue-key:SONAR-A --><!-- sonar-sync:end -->',
    user: {login: 'contributor'}, labels: ['Sonar Quality Issues']
  });
  await exercise([finding], fake);
  assert.equal(fake.created.length, 1);
  assert.equal(fake.issues[0].title, 'Manual issue');
});

test('fails closed on Sonar API errors without writing issues', {concurrency: false}, async () => {
  const fake = createFake();
  await assert.rejects(exercise([finding], fake, false), (error) => {
    assert.match(error.message, /HTTP 503/);
    assert.doesNotMatch(error.message, /Unavailable/);
    return true;
  });
  assert.equal(fake.created.length, 0);
});
