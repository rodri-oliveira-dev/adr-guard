'use strict';

const { test } = require('node:test');
const assert = require('node:assert/strict');
const sync = require('../sonar-quality-sync.cjs');

function createFake() {
  const issues = [];
  const created = [];
  const updated = [];
  const commented = [];
  const requested = [];
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
          Object.assign(issue, Object.fromEntries(Object.entries(args).filter(([, value]) => value !== undefined)));
          updated.push(args);
          return {data: issue};
        },
        createComment: async (args) => {
          commented.push(args);
          return {data: {}};
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
  return {github, core, issues, created, updated, commented, requested};
}

async function exercise(apiIssues, fake, ok = true, options = {}) {
  const originalFetch = global.fetch;
  const env = {SONAR_HOST_URL: process.env.SONAR_HOST_URL, SONAR_PROJECT_KEY: process.env.SONAR_PROJECT_KEY, SONAR_ISSUE_LABEL: process.env.SONAR_ISSUE_LABEL, SONAR_TOKEN: process.env.SONAR_TOKEN};
  process.env.SONAR_HOST_URL = 'https://sonarcloud.io';
  process.env.SONAR_PROJECT_KEY = 'test_project';
  process.env.SONAR_ISSUE_LABEL = 'Sonar Quality Issues';
  process.env.SONAR_TOKEN = 'fake-token';
  global.fetch = async (input) => {
    const url = new URL(input);
    fake.requested.push(url);
    const page = Number(url.searchParams.get('p'));
    const pageSize = Number(url.searchParams.get('ps'));
    const payload = options.responseForPage
      ? options.responseForPage(page, pageSize)
      : {
          issues: apiIssues.slice((page - 1) * pageSize, page * pageSize),
          rules: [],
          paging: {total: apiIssues.length, pageIndex: page, pageSize}
        };
    return {
      ok, status: ok ? 200 : 503,
      json: async () => payload,
      text: async () => 'Unavailable'
    };
  };
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

test('repeat runs close stale findings once and reopen the original issue', {concurrency: false}, async () => {
  const fake = createFake();
  await exercise([finding], fake);
  fake.issues[0].body += '\n## Human notes\nInvestigating in a follow-up.';
  await exercise([finding], fake);
  assert.equal(fake.created.length, 1);
  assert.equal(fake.updated.length, 0);
  assert.match(fake.issues[0].body, /Investigating in a follow-up/);

  await exercise([], fake);
  assert.equal(fake.issues[0].state, 'closed');
  assert.equal(fake.commented.length, 1);
  assert.match(fake.commented[0].body, /no longer returned.*OPEN/);
  assert.match(fake.commented[0].body, /does not independently/);

  await exercise([], fake);
  assert.equal(fake.commented.length, 1);
  assert.equal(fake.updated.length, 1);

  await exercise([finding], fake);
  assert.equal(fake.created.length, 1);
  assert.equal(fake.issues[0].state, 'open');
  assert.equal(fake.updated.length, 2);
  assert.equal(fake.commented.length, 1);
  assert.match(fake.issues[0].body, /Investigating in a follow-up/);
  assert.equal(fake.requested[0].searchParams.get('branch'), 'main');
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

test('does not close manually authored lookalikes or unrelated pull requests', {concurrency: false}, async () => {
  const fake = createFake();
  const marker = '<!-- sonar-sync:start --><!-- sonar-issue-key:SONAR-A --><!-- sonar-sync:end -->';
  fake.issues.push({
    number: 45, state: 'open', title: 'Manual issue', body: marker,
    user: {login: 'contributor'}, labels: ['Sonar Quality Issues']
  });
  fake.issues.push({
    number: 46, state: 'open', title: 'Pull request', body: marker,
    user: {login: 'github-actions[bot]'}, pull_request: {}, labels: ['Sonar Quality Issues']
  });
  await exercise([], fake);
  assert.equal(fake.updated.length, 0);
  assert.equal(fake.commented.length, 0);
});

test('fails closed on truncated Sonar pages without touching tracked issues', {concurrency: false}, async () => {
  const fake = createFake();
  await exercise([finding], fake);
  await assert.rejects(
    exercise([], fake, true, {
      responseForPage: (page, pageSize) => ({
        issues: [], rules: [], paging: {total: 501, pageIndex: page, pageSize}
      })
    }),
    /incomplete page/
  );
  assert.equal(fake.issues[0].state, 'open');
  assert.equal(fake.commented.length, 0);
});

test('fails closed on missing paging metadata without touching tracked issues', {concurrency: false}, async () => {
  const fake = createFake();
  await exercise([finding], fake);
  await assert.rejects(
    exercise([], fake, true, {responseForPage: () => ({issues: [], rules: []})}),
    /incomplete or inconsistent paging/
  );
  assert.equal(fake.issues[0].state, 'open');
  assert.equal(fake.commented.length, 0);
});

test('fails closed on malformed or duplicate finding keys', {concurrency: false}, async () => {
  const fake = createFake();
  await exercise([finding], fake);
  await assert.rejects(
    exercise([finding, finding], fake),
    /missing or duplicate finding key/
  );
  assert.equal(fake.issues[0].state, 'open');
  assert.equal(fake.commented.length, 0);
});
