import { spawn, spawnSync } from 'node:child_process';
import { access, mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises';
import { createServer } from 'node:net';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import os from 'node:os';
import process from 'node:process';
import sharp from 'sharp';

const websiteRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const repositoryRoot = resolve(websiteRoot, '..');
const extensionRoot = resolve(repositoryRoot, 'extensions/vscode');
const outputRoot = resolve(websiteRoot, 'public/images/vscode');
const cliName = process.platform === 'win32' ? 'adr-guard.exe' : 'adr-guard';
const cliPath = resolve(repositoryRoot, 'src/AdrGuard/bin/Release/net10.0', cliName);
const delay = (milliseconds) => new Promise((resolveDelay) => setTimeout(resolveDelay, milliseconds));

class CdpClient {
  constructor(url) {
    this.url = url;
    this.socket = undefined;
    this.nextId = 0;
    this.pending = new Map();
  }

  async connect() {
    this.socket = new WebSocket(this.url);
    this.socket.onmessage = (event) => {
      const message = JSON.parse(event.data);
      if (!message.id || !this.pending.has(message.id)) return;
      const pending = this.pending.get(message.id);
      this.pending.delete(message.id);
      clearTimeout(pending.timer);
      if (message.error) pending.reject(new Error(message.error.message));
      else pending.resolve(message.result);
    };
    await new Promise((resolveConnection, rejectConnection) => {
      this.socket.onopen = resolveConnection;
      this.socket.onerror = rejectConnection;
    });
  }

  call(method, params = {}) {
    if (!this.socket) throw new Error('CDP client is not connected.');
    return new Promise((resolveCall, rejectCall) => {
      const id = ++this.nextId;
      const timer = setTimeout(() => {
        this.pending.delete(id);
        rejectCall(new Error(`Timed out waiting for CDP method ${method}.`));
      }, 10_000);
      this.pending.set(id, { resolve: resolveCall, reject: rejectCall, timer });
      this.socket.send(JSON.stringify({ id, method, params }));
    });
  }

  close() {
    this.socket?.close();
  }
}

async function main() {
  ensureBuild('dotnet', ['build', 'src/AdrGuard/AdrGuard.csproj', '--configuration', 'Release'], repositoryRoot);
  ensureBuild(process.execPath, [npmCliPath(), 'run', 'build'], extensionRoot);

  const vscodePath = await findVsCode();
  const captureTempBase = resolve(process.env.ADR_GUARD_CAPTURE_TEMP?.trim() || os.tmpdir());
  await mkdir(captureTempBase, { recursive: true });
  const temporaryRoot = await mkdtemp(join(captureTempBase, 'adr-guard-vscode-capture-'));
  const workspace = join(temporaryRoot, 'adr-guard-demo');
  const userData = join(temporaryRoot, 'user-data');
  const extensions = join(temporaryRoot, 'extensions');
  const port = await availablePort();
  let child;
  let page;
  let webview;

  try {
    await createDemoWorkspace(workspace, userData);
    await mkdir(extensions, { recursive: true });
    await mkdir(outputRoot, { recursive: true });

    const launchEnvironment = { ...process.env };
    delete launchEnvironment.ELECTRON_RUN_AS_NODE;
    launchEnvironment.PATH = `${dirname(cliPath)}${process.platform === 'win32' ? ';' : ':'}${process.env.PATH ?? ''}`;
    child = spawn(vscodePath, [
      '--new-window',
      `--user-data-dir=${userData}`,
      `--extensions-dir=${extensions}`,
      `--extensionDevelopmentPath=${extensionRoot}`,
      '--disable-workspace-trust',
      '--skip-welcome',
      '--skip-release-notes',
      `--remote-debugging-port=${port}`,
      workspace,
    ], {
      cwd: repositoryRoot,
      env: launchEnvironment,
      stdio: 'ignore',
      windowsHide: true,
    });

    const version = await waitForJson(`http://127.0.0.1:${port}/json/version`, 30_000);
    console.log(`Connected to ${version.Browser ?? 'VS Code'}.`);

    const pageTarget = await waitForTarget(port, (target) => target.type === 'page', 30_000);
    page = new CdpClient(pageTarget.webSocketDebuggerUrl);
    await page.connect();
    console.log('Connected to the VS Code workbench renderer.');
    await page.call('Page.enable');
    await page.call('Runtime.enable');
    await delay(4_000);

    await runCommand(page, 'ADR Guard: Open ADR Guard');
    const webviewTarget = await waitForTarget(port, (target) => target.type === 'iframe' && target.url.includes('rodrioliveira.adr-guard'), 15_000);
    webview = new CdpClient(webviewTarget.webSocketDebuggerUrl);
    await webview.connect();
    console.log('Connected to the ADR Guard Webview renderer.');
    await webview.call('Runtime.enable');
    await waitForWebview(webview, '#adr-list');

    await capture(page, 'adr-list.png');

    await webviewEvaluate(webview, `
      const search = document.getElementById('search');
      search.value = 'observability';
      search.dispatchEvent(new Event('input', { bubbles: true }));
      const status = document.getElementById('status-filter');
      status.value = 'Proposed';
      status.dispatchEvent(new Event('change', { bubbles: true }));
      const sort = document.getElementById('sort');
      sort.value = 'title-asc';
      sort.dispatchEvent(new Event('change', { bubbles: true }));
    `);
    await delay(400);
    await capture(page, 'adr-search-filter.png');

    await resetList(webview);
    await clickButton(webview, 'New ADR');
    await waitForWebview(webview, '#adr-form');
    await fillCreationForm(webview, 'minimal');
    await waitForWebviewText(webview, '#preview', '# ');
    await capture(page, 'adr-create-minimal.png');
    await webviewEvaluate(webview, `document.getElementById('preview').scrollIntoView({ block: 'center' })`);
    await delay(300);
    await capture(page, 'adr-markdown-preview.png');

    await discardAndClickById(webview, 'create-back');
    await waitForWebview(webview, '#adr-list');
    await clickButton(webview, 'New ADR');
    await waitForWebview(webview, '#adr-form');
    await fillCreationForm(webview, 'extended');
    await waitForWebview(webview, '#content-decisionDrivers');
    await webviewEvaluate(webview, `document.getElementById('creation-content').scrollIntoView({ block: 'start' })`);
    await delay(500);
    await capture(page, 'adr-create-extended.png');

    await discardAndClickById(webview, 'create-back');
    await waitForWebview(webview, '#adr-list');
    await webviewEvaluate(webview, `
      [...document.querySelectorAll('.adr-row')]
        .find((row) => row.textContent.includes('managed message broker'))
        .querySelector('button').click()
    `);
    await waitForWebview(webview, '#adr-edit-form');
    await setValue(webview, 'edit-title', 'Use a managed message broker for domain events');
    await capture(page, 'adr-edit.png');

    await discardAndClickById(webview, 'edit-back');
    await waitForWebview(webview, '#adr-list');
    await clickById(webview, 'open-settings');
    await waitForWebview(webview, '#settings-form');
    await capture(page, 'adr-settings.png');

    await webviewEvaluate(webview, `document.getElementById('settings-validation').scrollIntoView({ block: 'start' })`);
    await delay(300);
    await capture(page, 'adr-validation-settings.png');

    await webviewEvaluate(webview, `
      const advanced = document.querySelector('.advanced-settings');
      advanced.open = true;
      document.getElementById('settings-cli').scrollIntoView({ block: 'start' });
    `);
    await delay(300);
    await capture(page, 'adr-cli-settings.png');

    await clickById(webview, 'settings-back');
    await waitForWebview(webview, '#adr-list');
    await page.call('Runtime.evaluate', {
      expression: `[...document.querySelectorAll('[aria-label="ADR Guard"]')][0]?.click()`,
    });
    await delay(1_000);
    await capture(page, 'adr-explorer.png');

    await writeInvalidDiagnosticFixture(workspace);
    await runCommand(page, 'ADR Guard: Validate ADRs');
    await delay(2_000);
    await chord(page, ['Control', 'Shift'], 'KeyM', 'm', 77);
    await delay(1_000);
    await capture(page, 'adr-problems.png');

    console.log(`Captured VS Code media in ${outputRoot}`);
  } finally {
    webview?.close();
    if (page) {
      try { await page.call('Browser.close'); } catch { /* host may already be closed */ }
      page.close();
    }
    if (child && child.exitCode === null) child.kill();
    await delay(500);
    await rm(temporaryRoot, { recursive: true, force: true, maxRetries: 3 });
  }
}

function ensureBuild(command, args, cwd) {
  const result = spawnSync(command, args, { cwd, stdio: 'inherit', shell: false });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`${command} ${args.join(' ')} failed with exit code ${result.status}.`);
}

function npmCliPath() {
  const npmCli = process.env.npm_execpath?.trim();
  if (!npmCli) throw new Error('The capture script must be started through npm so it can compile the extension.');
  return npmCli;
}

async function findVsCode() {
  const configured = process.env.ADR_GUARD_CAPTURE_VSCODE?.trim();
  const candidates = [
    configured,
    process.platform === 'win32'
      ? resolve(extensionRoot, '.vscode-test/vscode-win32-x64-archive-1.100.0/Code.exe')
      : undefined,
    process.platform === 'win32' && process.env.LOCALAPPDATA
      ? resolve(process.env.LOCALAPPDATA, 'Programs/Microsoft VS Code/Code.exe')
      : undefined,
    process.platform === 'win32' && process.env.ProgramFiles
      ? resolve(process.env.ProgramFiles, 'Microsoft VS Code/Code.exe')
      : undefined,
  ].filter(Boolean);
  for (const candidate of candidates) {
    try {
      await access(candidate);
      return candidate;
    } catch { /* try the next explicit installation */ }
  }
  throw new Error('VS Code was not found. Set ADR_GUARD_CAPTURE_VSCODE to a desktop Code executable or prepare the pinned extension test host.');
}

async function availablePort() {
  const server = createServer();
  await new Promise((resolveListen, rejectListen) => {
    server.once('error', rejectListen);
    server.listen(0, '127.0.0.1', resolveListen);
  });
  const address = server.address();
  const port = typeof address === 'object' && address ? address.port : undefined;
  await new Promise((resolveClose, rejectClose) => server.close((error) => error ? rejectClose(error) : resolveClose()));
  if (!port) throw new Error('Could not reserve a local debugging port.');
  return port;
}

async function waitForJson(url, timeout) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    try {
      const response = await fetch(url);
      if (response.ok) return await response.json();
    } catch { /* host is still starting */ }
    await delay(250);
  }
  throw new Error(`Timed out waiting for ${url}.`);
}

async function waitForTarget(port, predicate, timeout) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    const targets = await waitForJson(`http://127.0.0.1:${port}/json/list`, 2_000);
    const target = targets.find(predicate);
    if (target) return target;
    await delay(250);
  }
  throw new Error('Timed out waiting for the VS Code renderer target.');
}

async function runCommand(page, command) {
  await key(page, 'F1', 'F1', 112);
  await delay(350);
  await page.call('Input.insertText', { text: command });
  await delay(700);
  await key(page, 'Enter', 'Enter', 13);
  await delay(1_300);
}

async function key(client, keyValue, code, virtualKeyCode, modifiers = 0) {
  await client.call('Input.dispatchKeyEvent', {
    type: 'keyDown', key: keyValue, code, windowsVirtualKeyCode: virtualKeyCode,
    nativeVirtualKeyCode: virtualKeyCode, modifiers,
  });
  await client.call('Input.dispatchKeyEvent', {
    type: 'keyUp', key: keyValue, code, windowsVirtualKeyCode: virtualKeyCode,
    nativeVirtualKeyCode: virtualKeyCode, modifiers,
  });
}

async function chord(client, modifierKeys, code, keyValue, virtualKeyCode) {
  let modifiers = 0;
  for (const modifier of modifierKeys) {
    if (modifier === 'Control') modifiers |= 2;
    if (modifier === 'Shift') modifiers |= 8;
    await client.call('Input.dispatchKeyEvent', { type: 'keyDown', key: modifier, code: `${modifier}Left`, modifiers });
  }
  await key(client, keyValue, code, virtualKeyCode, modifiers);
  for (const modifier of [...modifierKeys].reverse()) {
    if (modifier === 'Control') modifiers &= ~2;
    if (modifier === 'Shift') modifiers &= ~8;
    await client.call('Input.dispatchKeyEvent', { type: 'keyUp', key: modifier, code: `${modifier}Left`, modifiers });
  }
}

async function webviewEvaluate(client, source, returnByValue = false) {
  const expression = `(() => { const document = globalThis.document.querySelector('iframe').contentDocument; ${source} })()`;
  const result = await client.call('Runtime.evaluate', { expression, returnByValue, awaitPromise: true });
  if (result.exceptionDetails) throw new Error(result.exceptionDetails.text ?? 'Webview evaluation failed.');
  return result.result?.value;
}

async function waitForWebview(client, selector, timeout = 10_000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    const found = await webviewEvaluate(client, `return Boolean(document.querySelector(${JSON.stringify(selector)}));`, true);
    if (found) return;
    await delay(150);
  }
  throw new Error(`Timed out waiting for Webview selector ${selector}.`);
}

async function waitForWebviewText(client, selector, expected, timeout = 10_000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    const found = await webviewEvaluate(client, `
      const node = document.querySelector(${JSON.stringify(selector)});
      return Boolean(node?.textContent.includes(${JSON.stringify(expected)}));
    `, true);
    if (found) return;
    await delay(150);
  }
  throw new Error(`Timed out waiting for ${selector} to contain ${expected}.`);
}

async function clickButton(client, label) {
  await webviewEvaluate(client, `
    const button = [...document.querySelectorAll('button')].find((candidate) => candidate.textContent.trim() === ${JSON.stringify(label)});
    if (!button) throw new Error('Button not found: ${label}');
    button.click();
  `);
}

async function clickById(client, id) {
  await webviewEvaluate(client, `
    const control = document.getElementById(${JSON.stringify(id)});
    if (!control) throw new Error('Control not found: ${id}');
    control.click();
  `);
}

async function discardAndClickById(client, id) {
  await webviewEvaluate(client, `document.defaultView.confirm = () => true;`);
  await clickById(client, id);
}

async function setValue(client, id, value, eventName = 'input') {
  await webviewEvaluate(client, `
    const control = document.getElementById(${JSON.stringify(id)});
    control.value = ${JSON.stringify(value)};
    control.dispatchEvent(new Event(${JSON.stringify(eventName)}, { bubbles: true }));
  `);
}

async function resetList(client) {
  await setValue(client, 'search', '');
  await setValue(client, 'status-filter', '', 'change');
  await setValue(client, 'sort', 'number-desc', 'change');
  await delay(300);
}

async function fillCreationForm(client, template) {
  await setValue(client, 'template', template, 'change');
  await waitForWebview(client, '#content-context');
  await setValue(client, 'title', template === 'minimal'
    ? 'Require idempotency keys for order creation'
    : 'Adopt regional failover for the public API');
  await setValue(client, 'content-context', template === 'minimal'
    ? 'Clients may retry order requests after network timeouts, which can create duplicate orders.'
    : 'The public API needs a documented recovery strategy for a regional platform outage.');
  await setValue(client, 'content-decision', template === 'minimal'
    ? 'Require a client-provided idempotency key and retain completed results for 24 hours.'
    : 'Use active-passive regional failover with explicit health checks and a controlled promotion runbook.');
  await setValue(client, 'content-consequences', template === 'minimal'
    ? 'Retries become safe within the retention window, with additional storage and key lifecycle handling.'
    : 'Recovery is predictable, but capacity, replication, and failover exercises add operational cost.');
  if (template === 'extended') {
    await waitForWebview(client, '#content-decisionDrivers');
    await setValue(client, 'content-decisionDrivers', 'Recovery time objective, data consistency, operational clarity, and cost.');
    await setValue(client, 'content-optionsConsidered', 'Single region, active-active regions, and active-passive failover.');
    await setValue(client, 'content-rationale', 'Active-passive meets the recovery target without introducing active-active write coordination.');
    await setValue(client, 'content-risks', 'Replication lag and an incorrect promotion can delay recovery; quarterly exercises verify the runbook.');
  }
  await delay(1_200);
}

async function capture(page, name) {
  const result = await page.call('Page.captureScreenshot', { format: 'png', fromSurface: true });
  const raw = Buffer.from(result.data, 'base64');
  await sharp(raw).png({ compressionLevel: 9, adaptiveFiltering: true }).toFile(resolve(outputRoot, name));
  console.log(`Captured ${name}`);
}

async function createDemoWorkspace(workspace, userData) {
  await mkdir(join(workspace, 'docs/adr'), { recursive: true });
  await mkdir(join(userData, 'User'), { recursive: true });
  await writeFile(join(userData, 'User/settings.json'), `${JSON.stringify({
    'adrGuard.ui.language': 'en-US',
    'adrGuard.creation.documentLanguage': 'en-US',
    'adrGuard.creation.defaultTemplate': 'minimal',
    'adrGuard.validation.directory': 'docs/adr',
    'adrGuard.validation.adrFormat': 'canonical',
    'adrGuard.validation.onSave': false,
    'workbench.colorTheme': 'Default Dark Modern',
    'workbench.startupEditor': 'none',
    'workbench.editor.enablePreview': false,
    'window.zoomLevel': 0,
    'git.openRepositoryInParentFolders': 'never',
  }, null, 2)}\n`);
  const documents = {
    '0001-adopt-api-versioning.md': adr(1, 'Adopt explicit API versioning', '2026-09-18', 'Accepted',
      'Public clients need a predictable migration window when response contracts change.',
      'Expose the major API version in the request path and publish a deprecation schedule.',
      'Clients can migrate deliberately while maintainers support overlapping versions during the announced window.'),
    '0002-standardize-service-observability.md': adr(2, 'Standardize service observability', '2026-10-03', 'Proposed',
      'Teams need consistent traces, metrics, and logs when investigating cross-service failures.',
      'Use OpenTelemetry conventions and a shared set of required resource attributes.',
      'Dashboards and alerts can correlate signals across services with less custom normalization.'),
    '0003-use-managed-message-broker.md': adr(3, 'Use a managed message broker', '2026-10-11', 'Deprecated',
      'The order workflow needs durable asynchronous delivery without operating broker infrastructure.',
      'Use the managed broker offered by the platform team for domain events.',
      'Operations are simplified, with explicit dependency on platform availability and quotas.'),
  };
  await Promise.all(Object.entries(documents).map(([name, contents]) =>
    writeFile(join(workspace, 'docs/adr', name), contents)));
}

async function writeInvalidDiagnosticFixture(workspace) {
  await writeFile(join(workspace, 'docs/adr/0004-record-cache-invalidation.md'),
    `# 4. Record cache invalidation\n\nDate: 2026-10-14\n\n## Status\n\nProposed\n\n## Context\n\nCache ownership and invalidation are inconsistent across services.\n\n## Consequences\n\nThe decision section is intentionally absent so the Problems panel can demonstrate a real CLI diagnostic.\n`);
  await delay(500);
}

function adr(number, title, date, status, context, decision, consequences) {
  return `# ${number}. ${title}\n\nDate: ${date}\n\n## Status\n\n${status}\n\n## Context\n\n${context}\n\n## Decision\n\n${decision}\n\n## Consequences\n\n${consequences}\n`;
}

await main();
