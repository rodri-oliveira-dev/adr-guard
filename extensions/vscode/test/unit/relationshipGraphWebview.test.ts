import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import vm from 'node:vm';
import { before, describe, it } from 'node:test';
import { relationshipGraphWebviewHtml } from '../../src/webview/relationshipGraphHtml';
import { parseRelationshipGraphMessage } from '../../src/webview/relationshipGraphMessages';

let source = '';
let parseHtml: (html: string) => Window & typeof globalThis;

describe('relationship graph Webview security', () => {
  before(async () => {
    ({ parseHTML: parseHtml } = await import('linkedom'));
    source = await readFile(path.resolve(__dirname, '../../../media/relationshipGraph.js'), 'utf8');
  });

  it('uses a restrictive CSP, nonce-protected local script, and escaped attributes', () => {
    const html = relationshipGraphWebviewHtml('vscode-webview:', 'local-script.js', 'local-style.css', 'en-US" onload=alert(1)', 'safe-nonce');
    assert.match(html, /default-src 'none'; img-src 'none'; font-src 'none'; connect-src 'none';/u);
    assert.match(html, /script-src 'nonce-safe-nonce'/u);
    assert.match(html, /<script nonce="safe-nonce" src="local-script\.js"><\/script>/u);
    assert.match(html, /lang="en-US&quot; onload=alert\(1\)"/u);
    assert.doesNotMatch(html, /lang="en-US" onload=/u);
    assert.doesNotMatch(html, /https?:\/\//u);
    assert.match(html, /id="graph-status"[^>]+role="status"[^>]+aria-live="polite"/u);
    assert.match(html, /<main id="app"><\/main>/u);
  });

  it('renders untrusted contract fields only with text DOM APIs', async () => {
    const script = await readFile(path.resolve(__dirname, '../../../media/relationshipGraph.js'), 'utf8');
    assert.doesNotMatch(script, /\.innerHTML|insertAdjacentHTML|eval\s*\(|new Function/u);
    assert.match(script, /textContent/u);
    assert.match(script, /createElementNS/u);
  });

  it('provides selection, filtering, explicit refresh, focus, and keyboard navigation without edit actions', () => {
    assert.match(source, /type: 'filter'/u);
    assert.match(source, /type: 'focus'/u);
    assert.match(source, /type: 'openAdr'/u);
    assert.match(source, /ArrowRight/u);
    assert.match(source, /aria-pressed/u);
    assert.match(source, /tabindex/u);
    assert.doesNotMatch(source, /createAdr|approveAdr|updateStatus|saveAdr/u);
  });

  it('keeps keyboard focus through selection and host rerenders while sending only explicit navigation actions', () => {
    const harness = createHarness();
    const first = requiredNode(harness.document, 'docs/adr/0001.md');
    first.dispatchEvent(domEvent(harness.window, 'click', { bubbles: true }));
    assert.equal(requiredElement(harness.document, 'graph-details').textContent.includes('ADR-1'), true);
    assert.equal(activePath(harness.document), 'docs/adr/0001.md');
    assert.equal(requiredElement(harness.document, 'graph-status').textContent, 'Selected ADR-1: First.');

    harness.click('graph-open-adr');
    harness.click('graph-focus');
    assert.deepEqual(harness.messages.slice(-2), [
      { type: 'openAdr', path: 'docs/adr/0001.md' },
      { type: 'focus', path: 'docs/adr/0001.md' },
    ]);

    const selected = requiredNode(harness.document, 'docs/adr/0001.md');
    selected.dispatchEvent(keyboardEvent(harness.window, 'ArrowRight'));
    assert.equal(activePath(harness.document), 'docs/adr/0002.md');
    requiredActiveElement(harness.document).dispatchEvent(keyboardEvent(harness.window, 'Enter'));
    assert.equal(requiredElement(harness.document, 'graph-details').textContent.includes('ADR-2'), true);
    assert.equal(activePath(harness.document), 'docs/adr/0002.md');

    harness.focus('status-filter');
    harness.dispatchGraph();
    assert.equal(requiredActiveElement(harness.document).id, 'status-filter');
    harness.change('status-filter', 'Accepted');
    assert.deepEqual(harness.messages.at(-1), { type: 'filter', query: '', status: 'Accepted', relationshipType: null });
    harness.dispatch({ ...graphMessage(), filters: { query: '', status: 'Accepted', type: null, focusPath: null } });
    assert.equal(requiredElement(harness.document, 'graph-status').textContent, 'Filters updated. 2 matching ADRs.');
    harness.click('graph-refresh');
    assert.deepEqual(harness.messages.at(-1), { type: 'refresh' });
  });

  it('restores pre-loading focus only when the user did not move focus during loading', () => {
    const harness = createHarness();
    harness.focus('status-filter');
    harness.dispatch({ type: 'loading', strings: graphMessage().strings });
    harness.dispatchGraph();
    assert.equal(requiredActiveElement(harness.document).id, 'status-filter');

    harness.dispatch({ type: 'loading', strings: graphMessage().strings });
    harness.focus('graph-status');
    harness.dispatchGraph();
    assert.equal(requiredActiveElement(harness.document).id, 'graph-status');
  });

  it('makes relationship direction visible in the legend as well as available as localized text', async () => {
    const style = await readFile(path.resolve(__dirname, '../../../media/relationshipGraph.css'), 'utf8');
    assert.match(style, /\.legend-line::after/u);
    assert.match(style, /border-left: 7px solid currentColor/u);
    const harness = createHarness();
    assert.match(harness.document.querySelector('.legend .direction')?.textContent ?? '', /declaring ADR/u);
  });

  it('strictly bounds extension-host messages and rejects unknown shapes', () => {
    assert.deepEqual(parseRelationshipGraphMessage({ type: 'ready' }), { type: 'ready' });
    assert.deepEqual(parseRelationshipGraphMessage({ type: 'openAdr', path: 'docs/adr/0001.md' }), { type: 'openAdr', path: 'docs/adr/0001.md' });
    assert.deepEqual(parseRelationshipGraphMessage({ type: 'filter', query: 'cache', status: null, relationshipType: 'depends-on' }), { type: 'filter', query: 'cache', status: null, relationshipType: 'depends-on' });
    for (const invalid of [
      null, [], { type: 'ready', extra: true }, { type: 'openAdr', path: '' }, { type: 'openAdr', path: 'x'.repeat(4_097) },
      { type: 'focus' }, { type: 'filter', query: '', status: '', relationshipType: null }, { type: 'filter', query: '', status: null, relationshipType: 'inferred' },
    ]) assert.equal(parseRelationshipGraphMessage(invalid), undefined);
  });

  it('uses editor theme variables for light and dark compatibility', async () => {
    const style = await readFile(path.resolve(__dirname, '../../../media/relationshipGraph.css'), 'utf8');
    assert.match(style, /color-scheme: light dark/u);
    assert.match(style, /var\(--vscode-editor-background\)/u);
    assert.match(style, /var\(--vscode-focusBorder\)/u);
    assert.doesNotMatch(style, /#[0-9a-f]{3,8}\b/iu);
  });
});

interface GraphHarness {
  readonly document: Document;
  readonly messages: Record<string, unknown>[];
  readonly window: Window;
  change(id: string, value: string): void;
  click(id: string): void;
  dispatch(message: Record<string, unknown>): void;
  focus(id: string): void;
  dispatchGraph(): void;
}

function createHarness(): GraphHarness {
  const window = parseHtml('<!doctype html><html><body><div id="graph-status"></div><main id="app"></main></body></html>');
  const document = window.document;
  const messages: Record<string, unknown>[] = [];
  let activeElement: Element | null = null;
  Object.defineProperty(document, 'activeElement', { configurable: true, get: () => activeElement });
  const setActiveElement = (value: Element): void => { activeElement = value; };
  Object.defineProperty(window.Element.prototype, 'focus', { configurable: true, value(this: Element) { setActiveElement(this); } });
  Object.defineProperty(window.HTMLSelectElement.prototype, 'value', {
    configurable: true,
    get(this: HTMLSelectElement): string { return this.getAttribute('data-test-value') ?? this.querySelector('option')?.value ?? ''; },
    set(this: HTMLSelectElement, value: string) { this.setAttribute('data-test-value', value); },
  });
  vm.runInNewContext(source, {
    acquireVsCodeApi: () => ({ postMessage: (message: Record<string, unknown>) => messages.push(structuredClone(message)) }),
    document,
    window,
  });
  document.dispatchEvent(domEvent(window, 'DOMContentLoaded'));
  dispatchGraph();
  return {
    document, messages, window,
    change: (id, value) => {
      const target = requiredElement(document, id) as HTMLSelectElement;
      target.value = value;
      target.dispatchEvent(domEvent(window, 'change', { bubbles: true }));
    },
    click: (id) => { const target = document.getElementById(id); assert.ok(target, `Missing element: ${id}`); target.click(); },
    dispatch,
    focus: (id) => {
      const target = document.getElementById(id);
      assert.ok(target, `Missing element: ${id}`);
      target.focus();
      target.dispatchEvent(domEvent(window, 'focusin', { bubbles: true }));
    },
    dispatchGraph,
  };

  function dispatchGraph(): void {
    dispatch(graphMessage());
  }

  function dispatch(message: Record<string, unknown>): void {
    const event = domEvent(window, 'message');
    Object.defineProperty(event, 'data', { value: message });
    window.dispatchEvent(event);
  }
}

function graphMessage(): Record<string, unknown> {
  const strings = new Proxy<Record<string, string>>({
    title: 'ADR Relationships', refresh: 'Refresh', workspaceChanges: 'Refresh explicitly', nodes: 'ADRs', relationships: 'Relationships', resolved: 'Resolved', unresolved: 'Unresolved', ambiguous: 'Ambiguous',
    filters: 'Graph filters', search: 'Search ADRs', searchPlaceholder: 'ID, title, or path', status: 'Status', allStatuses: 'All statuses', relationshipType: 'Relationship type', allRelationshipTypes: 'All relationship types', dependsOn: 'Depends on', supersedes: 'Supersedes', supersededBy: 'Superseded by', clearFilters: 'Clear filters',
    direction: 'Arrows point from the declaring ADR to the declared target.', broken: 'Broken reference', cycle: 'Supersession cycle', details: 'ADR details', selectHint: 'Select an ADR', unknownTitle: 'Unknown title', unknownStatus: 'Unknown status', format: 'Format', path: 'Path', openAdr: 'Open ADR', focus: 'Focus direct relationships', clearFocus: 'Show complete graph', keyboardHelp: 'Keyboard help', diagnostics: 'Diagnostics', noMatches: 'No matches', empty: 'Empty', noRelationships: 'No relationships', focused: 'Focused', visibleNodes: '{0} of {1}', visualLimit: '{0}', selectedAnnouncement: 'Selected {0}: {1}.', filtersAnnouncement: 'Filters updated. {0} matching ADRs.',
  }, { get: (target, key) => target[String(key)] ?? String(key) });
  return {
    type: 'graph', strings: { ...strings }, workspace: 'Workspace', directory: 'docs/adr',
    summary: { nodes: 2, relationships: 1, resolved: 1, unresolved: 0, ambiguous: 0, diagnostics: 0 },
    statuses: ['Accepted', 'Proposed'], filters: { query: '', status: null, type: null, focusPath: null }, visibleLimit: 100, diagnostics: [],
    layout: {
      width: 580, height: 160, totalMatchingNodes: 2, truncated: false,
      nodes: [
        { id: 'ADR-1', path: 'docs/adr/0001.md', title: 'First', status: 'Accepted', format: 'canonical', x: 40, y: 40, cycle: false, broken: false, ambiguous: false },
        { id: 'ADR-2', path: 'docs/adr/0002.md', title: 'Second', status: 'Proposed', format: 'madr-4', x: 360, y: 40, cycle: false, broken: false, ambiguous: false },
      ],
      edges: [{ source: { x: 260, y: 78 }, target: { x: 360, y: 78 }, sourcePath: 'docs/adr/0001.md', targetPath: 'docs/adr/0002.md', type: 'depends-on', resolution: 'resolved', declaredTarget: 'ADR-2', cycle: false }],
    },
  };
}

function requiredNode(document: Document, pathValue: string): Element {
  const node = [...document.querySelectorAll('.graph-node')].find((candidate) => candidate.getAttribute('data-path') === pathValue);
  assert.ok(node, `Missing graph node: ${pathValue}`);
  return node;
}

function keyboardEvent(window: Window, key: string): Event {
  const event = domEvent(window, 'keydown', { bubbles: true, cancelable: true });
  Object.defineProperty(event, 'key', { value: key });
  return event;
}

function domEvent(window: Window, type: string, options?: EventInit): Event {
  const EventConstructor = (window as unknown as { Event: new (eventType: string, eventInit?: EventInit) => Event }).Event;
  return new EventConstructor(type, options);
}

function requiredElement(document: Document, id: string): HTMLElement {
  const element = document.getElementById(id);
  assert.ok(element, `Missing element: ${id}`);
  return element;
}

function requiredActiveElement(document: Document): Element {
  const element = document.activeElement;
  assert.ok(element, 'Expected an active element');
  return element;
}

function activePath(document: Document): string | null {
  return requiredActiveElement(document).getAttribute('data-path');
}
