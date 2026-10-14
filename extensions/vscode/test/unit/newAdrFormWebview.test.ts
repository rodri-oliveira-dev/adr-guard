import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import vm from 'node:vm';
import { before, describe, it } from 'node:test';

interface PostedMessage {
  readonly type: string;
  readonly [key: string]: unknown;
}

interface WebviewHarness {
  readonly document: Document;
  readonly messages: PostedMessage[];
  readonly window: Window;
  confirmWith(value: boolean): void;
  dispatchHost(message: Record<string, unknown>): void;
  input(id: string, value: string): void;
  change(id: string, value: string): void;
  check(id: string, value: boolean): void;
  click(id: string): void;
  clickButton(label: string): void;
  submit(id: string): void;
  clearMessages(): void;
  runTimers(): void;
  setScroll(value: number): void;
  readonly lastScroll: number;
}

const workspace = 'file:///workspace';
const listState = { search: 'api', status: 'Accepted', sort: 'title-asc', scrollTop: 32 };
let source = '';
let parseHtml: (html: string) => Window & typeof globalThis;

describe('visual ADR form control events', () => {
  before(async () => {
    ({ parseHTML: parseHtml } = await import('linkedom'));
    source = await readFile(path.resolve(__dirname, '../../../media/newAdrForm.js'), 'utf8');
  });

  it('groups supported fields into keyboard-navigable creation and edit sections', () => {
    const creation = creationHarness();
    assert.deepEqual([...creation.document.querySelectorAll('.form-section-nav a')].map((link) => link.getAttribute('href')), [
      '#creation-setup', '#creation-content', '#creation-preview',
    ]);
    assert.equal(creation.document.querySelectorAll('#creation-content .requirement.required').length, 4);
    creation.input('title', 'Visible decision title');
    assert.equal(creation.document.getElementById('form-document-identity')?.textContent, 'Visible decision title');

    const edit = editHarness();
    assert.equal(edit.document.getElementById('edit-metadata')?.contains(edit.document.getElementById('edit-metadata-0')), true);
    assert.equal(edit.document.getElementById('edit-content')?.contains(edit.document.getElementById('edit-section-0')), true);
    assert.equal(edit.document.querySelectorAll('#edit-content .requirement.optional').length, 1);
  });

  it('keeps creation values when discard is refused and returns without mutations when confirmed', () => {
    const harness = creationHarness();
    harness.input('title', 'Keep this decision');
    harness.confirmWith(false);
    harness.click('create-back');

    assert.ok(harness.document.getElementById('adr-form'));
    assert.equal(valueOf(harness, 'title'), 'Keep this decision');
    assert.equal(harness.messages.length, 0);

    harness.confirmWith(true);
    harness.click('create-back');
    assert.deepEqual(harness.messages, [{ type: 'cancel' }]);
    assertNoMutationOrValidation(harness.messages);
  });

  it('cancels pristine creation without creating or validating an ADR', () => {
    for (const control of ['create-back', 'create-cancel']) {
      const harness = creationHarness();
      harness.click(control);
      assert.deepEqual(harness.messages, [{ type: 'cancel' }], control);
      assertNoMutationOrValidation(harness.messages);
    }
  });

  it('confirms dirty creation Cancel independently from Back', () => {
    const harness = creationHarness();
    harness.input('title', 'Keep this draft');
    harness.confirmWith(false);
    harness.click('create-cancel');
    assert.equal(valueOf(harness, 'title'), 'Keep this draft');
    assert.deepEqual(harness.messages, []);

    harness.confirmWith(true);
    harness.click('create-cancel');
    assert.deepEqual(harness.messages, [{ type: 'cancel' }]);
    assertNoMutationOrValidation(harness.messages);
  });

  it('keeps edit values when discard is refused and lets Back or Cancel leave without saving', () => {
    const cancelHarness = editHarness();
    cancelHarness.input('edit-title', 'Changed title');
    cancelHarness.confirmWith(false);
    cancelHarness.click('edit-cancel');
    assert.equal(valueOf(cancelHarness, 'edit-title'), 'Changed title');
    assert.deepEqual(cancelHarness.messages, []);

    cancelHarness.confirmWith(true);
    cancelHarness.click('edit-cancel');
    assert.deepEqual(cancelHarness.messages, [{ type: 'cancel' }]);
    assertNoMutationOrValidation(cancelHarness.messages);

    const backHarness = editHarness();
    backHarness.click('edit-back');
    assert.deepEqual(backHarness.messages, [{ type: 'cancel' }]);
    assertNoMutationOrValidation(backHarness.messages);

    const pristineCancelHarness = editHarness();
    pristineCancelHarness.click('edit-cancel');
    assert.deepEqual(pristineCancelHarness.messages, [{ type: 'cancel' }]);

    const dirtyBackHarness = editHarness();
    dirtyBackHarness.input('edit-title', 'Keep the changed title');
    dirtyBackHarness.confirmWith(false);
    dirtyBackHarness.click('edit-back');
    assert.equal(valueOf(dirtyBackHarness, 'edit-title'), 'Keep the changed title');
    assert.deepEqual(dirtyBackHarness.messages, []);
    dirtyBackHarness.confirmWith(true);
    dirtyBackHarness.click('edit-back');
    assert.deepEqual(dirtyBackHarness.messages, [{ type: 'cancel' }]);
    assertNoMutationOrValidation(dirtyBackHarness.messages);
  });

  it('creates one document per explicit submission and preserves values after an error', () => {
    const harness = creationHarness();
    harness.input('title', 'Adopt queues');
    harness.input('content-context', 'Requests need buffering.');
    harness.input('content-decision', 'Adopt a managed queue.');
    harness.input('content-consequences', 'Operations owns queue health.');

    harness.submit('adr-form');
    harness.submit('adr-form');
    const creates = harness.messages.filter((message) => message.type === 'create');
    assert.equal(creates.length, 1);
    assert.equal((creates[0]?.values as { title?: string }).title, 'Adopt queues');
    assert.equal(disabledOf(harness, 'create-back'), true);
    harness.click('create-back');
    harness.click('create-cancel');
    assert.equal(harness.messages.some((message) => message.type === 'cancel'), false);

    harness.dispatchHost({ type: 'error', message: 'Validation failed' });
    harness.dispatchHost({ type: 'idle', operation: 'create' });
    assert.equal(valueOf(harness, 'title'), 'Adopt queues');
    assert.equal(valueOf(harness, 'content-decision'), 'Adopt a managed queue.');
    harness.submit('adr-form');
    assert.equal(harness.messages.filter((message) => message.type === 'create').length, 2);
  });

  it('keeps Markdown preview read-only and distinct from validation or persistence', () => {
    const harness = creationHarness();
    harness.input('title', 'Preview queues');
    harness.input('content-context', 'Requests need buffering.');
    harness.input('content-decision', 'Adopt a managed queue.');
    harness.input('content-consequences', 'Operations owns queue health.');
    harness.runTimers();

    assert.deepEqual(harness.messages.map((message) => message.type), ['preview']);
    assertNoMutationOrValidation(harness.messages);
  });

  it('saves one edit at a time and preserves all values for retry after an error', () => {
    const harness = editHarness();
    harness.input('edit-title', 'Updated decision');
    harness.input('edit-section-0', 'Updated context');

    harness.submit('adr-edit-form');
    harness.submit('adr-edit-form');
    const saves = harness.messages.filter((message) => message.type === 'saveEdit');
    assert.equal(saves.length, 1);
    const editValues = saves[0]?.editValues as { title?: string; fields?: Record<string, string> };
    assert.equal(editValues.title, 'Updated decision');
    assert.equal(editValues.fields?.['section-0'], 'Updated context');
    assert.equal(disabledOf(harness, 'edit-cancel'), true);
    harness.click('edit-cancel');
    harness.click('edit-back');
    assert.equal(harness.messages.some((message) => message.type === 'cancel'), false);

    harness.dispatchHost({ type: 'editError', message: 'The ADR changed externally.' });
    harness.dispatchHost({ type: 'editIdle' });
    assert.equal(valueOf(harness, 'edit-title'), 'Updated decision');
    assert.equal(valueOf(harness, 'edit-section-0'), 'Updated context');
    harness.submit('adr-edit-form');
    assert.equal(harness.messages.filter((message) => message.type === 'saveEdit').length, 2);
  });

  it('restores list search, filters, ordering, selection, and scroll after navigation', () => {
    const harness = createHarness('list');
    harness.dispatchHost(listResult(listState));
    harness.setScroll(77);
    harness.clickButton('New ADR');
    const navigation = harness.messages.find((message) => message.type === 'newAdr');
    assert.deepEqual(navigation?.state, { ...listState, scrollTop: 77 });

    harness.dispatchHost({ type: 'showForm', workspace });
    harness.dispatchHost({ type: 'showList' });
    harness.dispatchHost(listResult());
    assert.equal(valueOf(harness, 'search'), 'api');
    assert.equal(valueOf(harness, 'status-filter'), 'Accepted');
    assert.equal(valueOf(harness, 'sort'), 'title-asc');
    assert.equal(valueOf(harness, 'list-workspace'), workspace);
    assert.equal(harness.lastScroll, 77);
  });

  it('opens settings in the same Webview and cancels pristine preferences without ADR operations', () => {
    const harness = createHarness('list');
    harness.dispatchHost(listResult(listState));
    harness.setScroll(81);
    harness.click('open-settings');
    const open = harness.messages.find((message) => message.type === 'openSettings');
    assert.deepEqual(open?.state, { ...listState, scrollTop: 81 });

    harness.dispatchHost({ type: 'showSettings', settings: settingsValues() });
    harness.clearMessages();
    harness.click('settings-cancel');
    assert.deepEqual(harness.messages, [{ type: 'cancelSettings', workspace, state: { ...listState, scrollTop: 81 } }]);
    assertNoMutationOrValidation(harness.messages);

    const backHarness = settingsHarness();
    backHarness.click('settings-back');
    assert.equal(backHarness.messages[0]?.type, 'cancelSettings');
    assertNoMutationOrValidation(backHarness.messages);
  });

  it('keeps unsaved settings when discard is refused and discards without CLI work when confirmed', () => {
    const harness = settingsHarness();
    harness.input('settings-directory', 'decisions/architecture');
    harness.confirmWith(false);
    harness.click('settings-cancel');
    assert.equal(valueOf(harness, 'settings-directory'), 'decisions/architecture');
    assert.equal(harness.messages.length, 0);

    harness.confirmWith(true);
    harness.click('settings-cancel');
    assert.equal(harness.messages[0]?.type, 'cancelSettings');
    assertNoMutationOrValidation(harness.messages);

    const backHarness = settingsHarness();
    backHarness.input('settings-directory', 'decisions/architecture');
    backHarness.confirmWith(false);
    backHarness.click('settings-back');
    assert.equal(valueOf(backHarness, 'settings-directory'), 'decisions/architecture');
    assert.equal(backHarness.messages.length, 0);
    backHarness.confirmWith(true);
    backHarness.click('settings-back');
    assert.equal(backHarness.messages[0]?.type, 'cancelSettings');
    assertNoMutationOrValidation(backHarness.messages);
  });

  it('saves settings once, preserves the draft after errors, and rejects unsafe directories locally', () => {
    const harness = settingsHarness();
    harness.input('settings-directory', '../outside');
    harness.submit('settings-form');
    assert.equal(harness.messages.length, 0);
    assert.match(harness.document.getElementById('error')?.textContent ?? '', /safe relative directory/u);

    harness.input('settings-directory', 'docs/decisions');
    harness.change('document-language', 'pt-BR');
    harness.change('default-template', 'extended');
    harness.submit('settings-form');
    harness.submit('settings-form');
    const saves = harness.messages.filter((message) => message.type === 'saveSettings');
    assert.equal(saves.length, 1);
    assert.deepEqual(saves[0]?.settings, {
      ...settingsValues(), directory: 'docs/decisions', documentLanguage: 'pt-BR', defaultTemplate: 'extended',
    });
    assertNoMutationOrValidation(harness.messages);
    assert.equal(disabledOf(harness, 'settings-back'), true);
    assert.equal(disabledOf(harness, 'settings-cancel'), true);
    harness.click('settings-back');
    harness.click('settings-cancel');
    assert.equal(harness.messages.some((message) => message.type === 'cancelSettings'), false);

    harness.dispatchHost({ type: 'settingsError', message: 'Could not persist settings' });
    harness.dispatchHost({ type: 'settingsIdle' });
    assert.equal(valueOf(harness, 'settings-directory'), 'docs/decisions');
    harness.submit('settings-form');
    assert.equal(harness.messages.filter((message) => message.type === 'saveSettings').length, 2);
  });

  it('returns successful create, edit, and settings submissions to the preserved refreshed list', () => {
    const created = creationHarness();
    created.input('title', 'Adopt queues');
    created.input('content-context', 'Requests need buffering.');
    created.input('content-decision', 'Adopt a managed queue.');
    created.input('content-consequences', 'Operations owns queue health.');
    created.submit('adr-form');
    created.dispatchHost({ type: 'created', workspace, relativePath: '0002-adopt-queues.md' });
    created.dispatchHost({ type: 'showList', state: listState });
    created.dispatchHost(listResult(listState, '0002-adopt-queues.md', 'created'));
    assert.ok(created.document.querySelector('.adr-row.created'));
    assert.equal(valueOf(created, 'search'), 'api');

    const edited = editHarness();
    edited.input('edit-title', 'Updated decision');
    edited.submit('adr-edit-form');
    edited.dispatchHost({ type: 'edited', workspace, relativePath: '0001-current.md' });
    edited.dispatchHost({ type: 'showList', state: listState });
    edited.dispatchHost(listResult(listState, '0001-current.md', 'updated'));
    assert.ok(edited.document.querySelector('.adr-row.created'));
    assert.equal(valueOf(edited, 'status-filter'), 'Accepted');

    const settings = settingsHarness();
    settings.change('ui-language', 'pt-BR');
    settings.submit('settings-form');
    settings.dispatchHost({ type: 'settingsSaved', settings: { ...settingsValues(), uiLanguage: 'pt-BR' } });
    settings.dispatchHost({ type: 'showList', state: listState });
    settings.dispatchHost(listResult(listState));
    assert.ok(settings.document.getElementById('adr-list'));
    assert.equal(valueOf(settings, 'sort'), 'title-asc');
  });

  it('previews the Webview language independently and applies persisted defaults to new ADRs', () => {
    const settings = settingsHarness();
    settings.change('ui-language', 'pt-BR');
    assert.equal(settings.document.querySelector('h1')?.textContent, 'Configurações');
    assert.equal(settings.messages.length, 0);

    const creation = createHarness('form', { documentLanguage: 'pt-BR', defaultTemplate: 'extended' });
    assert.equal(valueOf(creation, 'culture'), 'pt-BR');
    assert.equal(valueOf(creation, 'template'), 'extended');
  });

  it('restores persisted preferences when settings are reopened', () => {
    const harness = settingsHarness();
    const persisted = { ...settingsValues(), documentLanguage: 'pt-BR', directory: 'architecture/decisions', defaultTemplate: 'minimal' };
    harness.dispatchHost({ type: 'settingsSaved', settings: persisted });
    harness.dispatchHost({ type: 'showList', state: listState });
    harness.dispatchHost(listResult());
    harness.dispatchHost({ type: 'showSettings', settings: persisted });

    assert.equal(valueOf(harness, 'document-language'), 'pt-BR');
    assert.equal(valueOf(harness, 'settings-directory'), 'architecture/decisions');
    assert.equal(valueOf(harness, 'default-template'), 'minimal');
  });

  it('preserves debounce while automatic validation is disabled and validates numeric limits', () => {
    const harness = settingsHarness();
    assert.equal(disabledOf(harness, 'validation-debounce'), true);
    harness.check('validation-on-save', true);
    assert.equal(disabledOf(harness, 'validation-debounce'), false);
    harness.input('validation-debounce', '1200');
    harness.check('validation-on-save', false);
    assert.equal(disabledOf(harness, 'validation-debounce'), true);
    assert.equal(valueOf(harness, 'validation-debounce'), '1200');
    harness.submit('settings-form');
    const saved = harness.messages.find((message) => message.type === 'saveSettings')?.settings as Record<string, unknown>;
    assert.equal(saved.onSave, false);
    assert.equal(saved.debounceMilliseconds, 1200);

    const invalid = settingsHarness();
    invalid.check('validation-on-save', true);
    invalid.input('validation-debounce', '199');
    invalid.input('cli-timeout', '120001');
    invalid.submit('settings-form');
    assert.equal(invalid.messages.some((message) => message.type === 'saveSettings'), false);
    assert.match(invalid.document.getElementById('error')?.textContent ?? '', /accepted limits/u);
  });

  it('checks CLI installation only after the explicit control event and displays its version', () => {
    const harness = settingsHarness();
    assert.equal(harness.messages.length, 0);
    harness.click('check-settings-installation');
    harness.click('check-settings-installation');
    assert.deepEqual(harness.messages.map((message) => message.type), ['checkSettingsInstallation']);
    assertNoMutationOrValidation(harness.messages);
    assert.equal(disabledOf(harness, 'check-settings-installation'), true);

    harness.dispatchHost({ type: 'installationResult', result: { available: true, version: '1.7.0', source: 'path', message: 'ADR Guard 1.7.0 is available (path).' } });
    harness.dispatchHost({ type: 'installationCheckIdle' });
    assert.match(harness.document.getElementById('installation-status')?.textContent ?? '', /1\.7\.0/u);
    assert.equal(disabledOf(harness, 'check-settings-installation'), false);
  });

  it('rejects unsafe baseline, Git reference, and executable values from real controls', () => {
    for (const [id, value] of [
      ['validation-baseline', '../outside.json'], ['validation-base-reference', '--all'], ['cli-path', 'workspace/tool'],
    ] as const) {
      const harness = settingsHarness();
      harness.input(id, value);
      harness.submit('settings-form');
      assert.equal(harness.messages.some((message) => message.type === 'saveSettings'), false, id);
    }
  });
});

function settingsValues(): Record<string, unknown> {
  return {
    workspace, uiLanguage: 'auto', documentLanguage: 'en-US', directory: 'docs/adr',
    adrFormat: 'canonical', defaultTemplate: 'configured',
    onSave: false, debounceMilliseconds: 750, baseline: '.adrguard-baseline.json', baseReference: 'main',
    cliPath: '', timeoutMilliseconds: 15000, maxOutputBytes: 262144,
  };
}

function settingsHarness(): WebviewHarness {
  const harness = createHarness('list');
  harness.dispatchHost(listResult());
  harness.dispatchHost({ type: 'showSettings', settings: settingsValues() });
  harness.clearMessages();
  return harness;
}

function creationHarness(): WebviewHarness {
  const harness = createHarness('form');
  harness.dispatchHost({
    type: 'templateLoaded',
    response: {
      operation: 'describe', written: false,
      template: {
        name: 'minimal', compatible: true,
        fields: [
          { id: 'context', required: true },
          { id: 'decision', required: true },
          { id: 'consequences', required: true },
        ],
      },
    },
  });
  harness.clearMessages();
  return harness;
}

function editHarness(): WebviewHarness {
  const harness = createHarness('list');
  harness.dispatchHost({
    type: 'editLoaded', workspace, relativePath: '0001-current.md',
    response: {
      fingerprint: 'A'.repeat(64), title: 'Current decision',
      fields: [
        { id: 'metadata-0', label: 'Status', kind: 'status', required: true, value: 'Accepted', options: ['Proposed', 'Accepted'] },
        { id: 'section-0', label: 'Context', kind: 'section', required: true, value: 'Current context' },
        { id: 'section-1', label: 'Consequences', kind: 'section', required: false, value: '' },
      ],
    },
  });
  harness.clearMessages();
  return harness;
}

function createHarness(
  initialScreen: 'list' | 'form',
  defaults: { readonly documentLanguage: string; readonly defaultTemplate: string } = { documentLanguage: 'en-US', defaultTemplate: 'configured' },
): WebviewHarness {
  const window = parseHtml('<!doctype html><html><body><main id="app"></main></body></html>');
  const document = window.document;
  const messages: PostedMessage[] = [];
  let confirmResult = true;
  let state: unknown;
  let scrollY = 0;
  let lastScroll = 0;
  let timer = 0;
  const timers = new Map<number, () => void>();
  const windowRecord = window as unknown as Record<string, unknown>;
  windowRecord.requestAnimationFrame = (callback: () => void): number => { callback(); return 1; };
  windowRecord.scrollTo = (_left: number, top: number): void => { lastScroll = top; };
  Object.defineProperty(window, 'scrollY', { configurable: true, get: () => scrollY });
  Object.defineProperty(window.HTMLSelectElement.prototype, 'value', {
    configurable: true,
    get(this: HTMLSelectElement): string {
      return this.getAttribute('data-test-value') ?? this.querySelector('option')?.value ?? '';
    },
    set(this: HTMLSelectElement, value: string) {
      this.setAttribute('data-test-value', value);
    },
  });
  Object.defineProperty(window.HTMLElement.prototype, 'reportValidity', {
    configurable: true,
    value(this: HTMLElement): boolean {
      return [...this.querySelectorAll<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>('[required]')]
        .every((control) => !control.required || control.value.trim().length > 0);
    },
  });
  const vscode = {
    getState: (): unknown => state,
    setState: (value: unknown): void => { state = clone(value); },
    postMessage: (message: PostedMessage): void => { messages.push(clone(message)); },
  };
  vm.runInNewContext(source, {
    acquireVsCodeApi: () => vscode,
    clearTimeout: (id: number) => { timers.delete(id); },
    confirm: () => confirmResult,
    document,
    setTimeout: (callback: () => void) => { timer += 1; timers.set(timer, callback); return timer; },
    window,
  });
  dispatch({
    type: 'initialize', trusted: true,
    workspaces: [{ id: workspace, name: 'Workspace', directory: 'docs/adr', ...defaults }],
    initialWorkspace: workspace, initialInterfacePreference: 'auto', editorInterfaceLanguage: 'en-US', hostPlatform: process.platform, initialScreen,
    strings: { 'en-US': {}, 'pt-BR': { Settings: 'Configurações' } },
  });

  return {
    document, messages, window,
    confirmWith: (value) => { confirmResult = value; },
    dispatchHost: dispatch,
    input: (id, value) => {
      const control = requiredElement(id) as unknown as { value: string; dispatchEvent(event: unknown): void };
      control.value = value;
      control.dispatchEvent(new window.Event('input', { bubbles: true }));
    },
    change: (id, value) => {
      const control = requiredElement(id) as unknown as { value: string; dispatchEvent(event: unknown): void };
      control.value = value;
      control.dispatchEvent(new window.Event('change', { bubbles: true }));
    },
    check: (id, value) => {
      const control = requiredElement(id) as unknown as { checked: boolean; dispatchEvent(event: unknown): void };
      control.checked = value;
      control.dispatchEvent(new window.Event('change', { bubbles: true }));
    },
    click: (id) => { (requiredElement(id) as unknown as { click(): void }).click(); },
    clickButton: (label) => {
      const control = [...document.querySelectorAll('button')].find((candidate) => candidate.textContent === label);
      assert.ok(control, `Missing button: ${label}`);
      control.click();
    },
    submit: (id) => { requiredElement(id).dispatchEvent(new window.Event('submit', { bubbles: true, cancelable: true })); },
    clearMessages: () => { messages.splice(0); },
    runTimers: () => {
      const pending = [...timers.entries()];
      timers.clear();
      pending.forEach(([, callback]) => callback());
    },
    setScroll: (value) => { scrollY = value; },
    get lastScroll() { return lastScroll; },
  };

  function dispatch(message: Record<string, unknown>): void {
    const event = new window.Event('message');
    Object.defineProperty(event, 'data', { value: message });
    window.dispatchEvent(event);
  }

  function requiredElement(id: string) {
    const element = document.getElementById(id);
    assert.ok(element, `Missing element: ${id}`);
    return element;
  }
}

function listResult(
  state?: typeof listState,
  highlightedPath?: string,
  highlightKind?: 'created' | 'updated',
): Record<string, unknown> {
  const relativePath = highlightedPath ?? '0001-api.md';
  return {
    type: 'listResult', workspace, directory: 'docs/adr',
    entries: [{ relativePath, id: relativePath.startsWith('0002') ? '0002' : '0001', title: 'Public API', status: 'Accepted', date: '2026-10-14' }],
    ...(state === undefined ? {} : { state }),
    ...(highlightedPath === undefined ? {} : { createdPath: highlightedPath, highlightKind }),
  };
}

function valueOf(harness: WebviewHarness, id: string): string {
  const element = harness.document.getElementById(id) as unknown as { value?: string } | null;
  assert.ok(element, `Missing element: ${id}`);
  return element.value ?? '';
}

function disabledOf(harness: WebviewHarness, id: string): boolean {
  const element = harness.document.getElementById(id) as unknown as { disabled?: boolean } | null;
  assert.ok(element, `Missing element: ${id}`);
  return element.disabled === true;
}

function assertNoMutationOrValidation(messages: readonly PostedMessage[]): void {
  assert.equal(messages.some((message) => ['create', 'saveEdit', 'traditional', 'initializeRepository', 'check'].includes(message.type)), false);
}

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}
