'use strict';

const fs = require('node:fs');

const marker = '<!-- adr-guard:sonar-quality-gate -->';
const reportTaskPath = '.sonarqube/out/.sonar/report-task.txt';
const maximumReportTaskBytes = 16 * 1024;
const maximumConditions = 100;

function readCeTaskId() {
  let stat;
  try {
    stat = fs.statSync(reportTaskPath);
  } catch {
    return '';
  }

  if (!stat.isFile() || stat.size <= 0 || stat.size > maximumReportTaskBytes) {
    return '';
  }

  try {
    const report = fs.readFileSync(reportTaskPath, 'utf8');
    const taskId = report.match(/^ceTaskId=([^\r\n]+)$/m)?.[1]?.trim() ?? '';
    return /^[A-Za-z0-9_-]{1,128}$/.test(taskId) ? taskId : '';
  } catch {
    return '';
  }
}

async function sonarGet(path, params, token, fetchImpl) {
  const url = new URL(path, 'https://sonarcloud.io');
  for (const [key, value] of Object.entries(params)) {
    url.searchParams.set(key, value);
  }

  const response = await fetchImpl(url, {
    headers: {
      Accept: 'application/json',
      Authorization: `Bearer ${token}`,
    },
  });

  if (!response.ok) {
    throw new Error(`Sonar API ${path} failed with HTTP ${response.status}.`);
  }

  return response.json();
}

function boundedMarkdown(value, fallback) {
  const text = String(value ?? fallback)
    .replace(/\r?\n/g, ' ')
    .replace(/\|/g, '\\|')
    .replace(/`/g, '\\`');
  return text.length <= 160 ? text : `${text.slice(0, 157)}...`;
}

async function resolveQualityGate({ceTaskId, token, fetchImpl}) {
  const taskResult = await sonarGet('/api/ce/task', {id: ceTaskId}, token, fetchImpl);
  const task = taskResult?.task;
  const taskStatus = typeof task?.status === 'string' ? task.status : '';
  const taskMatches = task?.id === undefined || task.id === ceTaskId;
  if (!taskMatches || !/^[A-Z_]{1,32}$/.test(taskStatus)) {
    return {
      status: 'UNKNOWN',
      conditions: [],
      detail: 'The current Sonar Compute Engine task response is invalid.',
    };
  }

  if (taskStatus !== 'SUCCESS') {
    return {
      status: 'UNKNOWN',
      conditions: [],
      detail: `The current Sonar Compute Engine task finished with status ${taskStatus}; no Quality Gate result is available.`,
    };
  }

  const analysisId = typeof task.analysisId === 'string' ? task.analysisId.trim() : '';
  if (!/^[A-Za-z0-9_-]{1,128}$/.test(analysisId)) {
    return {
      status: 'UNKNOWN',
      conditions: [],
      detail: 'The current Sonar Compute Engine task did not provide a valid analysis ID.',
    };
  }

  const gateResult = await sonarGet(
    '/api/qualitygates/project_status',
    {analysisId},
    token,
    fetchImpl,
  );
  const gateStatus = gateResult?.projectStatus?.status;
  if (typeof gateStatus !== 'string' || !/^[A-Z_]{1,32}$/.test(gateStatus)) {
    return {
      status: 'UNKNOWN',
      conditions: [],
      detail: 'The Quality Gate response for the current analysis is invalid.',
    };
  }

  return {
    status: gateStatus,
    conditions: Array.isArray(gateResult.projectStatus.conditions)
      ? gateResult.projectStatus.conditions.slice(0, maximumConditions)
      : [],
    detail: '',
  };
}

module.exports = async function updateQualityGate({
  github,
  context,
  core,
  fetchImpl = global.fetch,
  readTaskId = readCeTaskId,
}) {
  const {owner, repo} = context.repo;
  const pr = context.issue.number;
  const runUrl = `${process.env.GITHUB_SERVER_URL}/${process.env.GITHUB_REPOSITORY}/actions/runs/${process.env.GITHUB_RUN_ID}`;
  const projectKey = process.env.SONAR_PROJECT_KEY;
  const token = process.env.SONAR_TOKEN;
  const dashboard = `https://sonarcloud.io/project/overview?id=${encodeURIComponent(projectKey || '')}&pullRequest=${pr}`;
  const scanResult = process.env.SONAR_SCAN_RESULT;
  let ceTaskId = '';
  try {
    ceTaskId = readTaskId();
  } catch {
    // Missing or unreadable evidence remains UNKNOWN and still refreshes the comment.
  }

  let result = {
    status: 'UNKNOWN',
    conditions: [],
    detail: 'Scanner/coverage analysis did not produce verifiable evidence for the current run.',
  };

  if (!['success', 'failure'].includes(scanResult) || !ceTaskId) {
    result.detail = 'Scanner/coverage analysis did not finish with a valid current Compute Engine task. Inspect the workflow run.';
  } else if (!token || !projectKey) {
    result.detail = 'Sonar configuration is unavailable.';
  } else {
    core.setSecret(token);
    try {
      result = await resolveQualityGate({ceTaskId, token, fetchImpl});
    } catch {
      result = {
        status: 'UNKNOWN',
        conditions: [],
        detail: 'Quality Gate lookup failed for the current analysis. Inspect the workflow run.',
      };
    }
  }

  const table = result.conditions.length
    ? ['| Metric | Result | Actual | Threshold |', '| --- | --- | --- | --- |'].concat(
        result.conditions.map((condition) =>
          `| \`${boundedMarkdown(condition?.metricKey, 'unknown')}\` | ${boundedMarkdown(condition?.status, 'UNKNOWN')} | ${boundedMarkdown(condition?.actualValue, 'n/a')} | ${boundedMarkdown(condition?.errorThreshold, 'n/a')} |`,
        ),
      ).join('\n')
    : 'No computed conditions available.';
  const body = [
    marker,
    '### ADR Guard — SonarQube Cloud Quality Gate',
    '',
    `**Status: ${result.status}**`,
    '',
    result.detail,
    '',
    table,
    '',
    `- [SonarQube Cloud](${dashboard})`,
    `- [Workflow run](${runUrl})`,
  ].join('\n');

  const comments = await github.paginate(github.rest.issues.listComments, {
    owner,
    repo,
    issue_number: pr,
    per_page: 100,
  });
  const previous = comments.find(
    (comment) => comment.user?.login === 'github-actions[bot]' && comment.body?.includes(marker),
  );
  if (previous) {
    if (previous.body !== body) {
      await github.rest.issues.updateComment({owner, repo, comment_id: previous.id, body});
    }
  } else {
    await github.rest.issues.createComment({owner, repo, issue_number: pr, body});
  }

  if (result.status !== 'OK') {
    core.setFailed(`Sonar Quality Gate status ${result.status}.`);
  }
};

module.exports.readCeTaskId = readCeTaskId;
module.exports.resolveQualityGate = resolveQualityGate;
