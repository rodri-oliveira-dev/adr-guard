/* global acquireVsCodeApi, document, window */
(function () {
  'use strict';
  const vscode = acquireVsCodeApi();
  const app = document.getElementById('app');
  const statusRegion = document.getElementById('graph-status');
  const svgNamespace = 'http://www.w3.org/2000/svg';
  let strings = {};
  let currentMessage;
  let selectedPath;
  let requestedFocusPath;
  let focusBeforeLoading;
  let focusChangedDuringLoading = false;
  let loading = false;

  function element(name, attributes, text) {
    const node = document.createElement(name);
    for (const [key, value] of Object.entries(attributes || {})) node.setAttribute(key, String(value));
    if (text !== undefined) node.textContent = text;
    return node;
  }
  function svgElement(name, attributes, text) {
    const node = document.createElementNS(svgNamespace, name);
    for (const [key, value] of Object.entries(attributes || {})) node.setAttribute(key, String(value));
    if (text !== undefined) node.textContent = text;
    return node;
  }
  function action(label, type, attributes) {
    const button = element('button', { type: 'button', ...attributes }, label);
    button.addEventListener('click', () => vscode.postMessage({ type }));
    return button;
  }
  function state(title, detail, retry) {
    app.replaceChildren();
    const section = element('section', { class: 'state', role: retry ? 'alert' : 'status' });
    section.append(element('h1', {}, title), element('p', {}, detail));
    if (retry) section.append(action(strings.retry || 'Retry', 'refresh', { id: 'graph-retry' }));
    app.append(section);
  }
  function header(message) {
    const node = element('header', { class: 'graph-header' });
    const heading = element('div');
    heading.append(element('h1', {}, strings.title), element('p', { class: 'directory' }, `${message.workspace} · ${message.directory}`));
    const refresh = action(strings.refresh, 'refresh', { id: 'graph-refresh', 'aria-describedby': 'workspace-refresh-note' });
    node.append(heading, refresh);
    return node;
  }
  function refreshNote() { return element('p', { id: 'workspace-refresh-note', class: 'refresh-note' }, strings.workspaceChanges); }
  function metrics(summary) {
    const node = element('dl', { class: 'metrics' });
    [[strings.nodes, summary.nodes], [strings.relationships, summary.relationships], [strings.resolved, summary.resolved], [strings.unresolved, summary.unresolved], [strings.ambiguous, summary.ambiguous]].forEach(([label, value]) => {
      const item = element('div'); item.append(element('dt', {}, label), element('dd', {}, value)); node.append(item);
    });
    return node;
  }
  function filters(message) {
    const section = element('section', { class: 'filters', 'aria-label': strings.filters });
    const searchLabel = element('label', {}, strings.search);
    const search = element('input', { id: 'graph-search', type: 'search', maxlength: '256', placeholder: strings.searchPlaceholder, value: message.filters.query });
    searchLabel.append(search);
    const statusLabel = element('label', {}, strings.status);
    const status = element('select', { id: 'status-filter' });
    status.append(element('option', { value: '' }, strings.allStatuses));
    message.statuses.forEach((value) => status.append(element('option', { value }, value)));
    status.value = message.filters.status || '';
    statusLabel.append(status);
    const typeLabel = element('label', {}, strings.relationshipType);
    const type = element('select', { id: 'relationship-filter' });
    [['', strings.allRelationshipTypes], ['depends-on', strings.dependsOn], ['supersedes', strings.supersedes], ['superseded-by', strings.supersededBy]].forEach(([value, label]) => type.append(element('option', { value }, label)));
    type.value = message.filters.type || '';
    typeLabel.append(type);
    const apply = () => vscode.postMessage({ type: 'filter', query: search.value, status: status.value || null, relationshipType: type.value || null });
    search.addEventListener('change', apply); status.addEventListener('change', apply); type.addEventListener('change', apply);
    const clear = element('button', { id: 'graph-clear-filters', type: 'button', class: 'secondary' }, strings.clearFilters);
    clear.addEventListener('click', () => vscode.postMessage({ type: 'filter', query: '', status: null, relationshipType: null }));
    section.append(searchLabel, statusLabel, typeLabel, clear);
    return section;
  }
  function legend() {
    const node = element('section', { class: 'legend', 'aria-label': strings.relationships });
    [['depends-on', strings.dependsOn], ['supersedes', strings.supersedes], ['superseded-by', strings.supersededBy], ['unresolved', strings.broken], ['ambiguous', strings.ambiguous], ['cycle', strings.cycle]].forEach(([kind, label]) => {
      const item = element('span'); item.append(element('i', { class: `legend-line ${kind}`, 'aria-hidden': 'true' }), document.createTextNode(label)); node.append(item);
    });
    node.append(element('p', { class: 'direction' }, strings.direction));
    return node;
  }
  function edgePath(edge) {
    const target = edge.target || { x: edge.source.x + 72, y: edge.source.y };
    const curve = Math.max(32, Math.abs(target.x - edge.source.x) / 2);
    return `M ${edge.source.x} ${edge.source.y} C ${edge.source.x + curve} ${edge.source.y}, ${target.x - curve} ${target.y}, ${target.x} ${target.y}`;
  }
  function addMarker(defs, id, className) {
    const marker = svgElement('marker', { id, viewBox: '0 0 10 10', refX: '9', refY: '5', markerWidth: '7', markerHeight: '7', orient: 'auto-start-reverse' });
    marker.append(svgElement('path', { d: 'M 0 0 L 10 5 L 0 10 z', class: className })); defs.append(marker);
  }
  function graph(layout) {
    const viewport = element('div', { class: 'graph-viewport', role: 'region', 'aria-label': strings.relationships, 'aria-describedby': 'graph-keyboard-help' });
    const svg = svgElement('svg', { viewBox: `0 0 ${layout.width} ${layout.height}`, width: layout.width, height: layout.height });
    const defs = svgElement('defs'); addMarker(defs, 'arrow-depends', 'marker depends-on'); addMarker(defs, 'arrow-supersedes', 'marker supersedes'); addMarker(defs, 'arrow-superseded', 'marker superseded-by'); svg.append(defs);
    const edges = svgElement('g', { class: 'edges', 'aria-hidden': 'true' });
    layout.edges.forEach((edge) => {
      const classes = `edge ${edge.type} ${edge.resolution}${edge.cycle ? ' cycle' : ''}`;
      const path = svgElement('path', { d: edgePath(edge), class: classes, 'marker-end': `url(#arrow-${markerForType(edge.type)})` });
      path.append(svgElement('title', {}, `${labelForType(edge.type)} · ${edge.resolution}: ${edge.declaredTarget}`)); edges.append(path);
      if (!edge.target) {
        edges.append(svgElement('circle', { cx: edge.source.x + 72, cy: edge.source.y, r: '7', class: 'unresolved-target' }));
        edges.append(svgElement('text', { x: edge.source.x + 72, y: edge.source.y + 4, class: 'unresolved-mark', 'text-anchor': 'middle' }, '!'));
      }
    });
    svg.append(edges);
    const nodes = svgElement('g', { class: 'nodes' });
    const nodeElements = [];
    layout.nodes.forEach((item, index) => {
      const problemLabels = [item.cycle ? strings.cycle : '', item.broken ? strings.broken : '', item.ambiguous ? strings.ambiguous : ''].filter(Boolean);
      const selected = item.path === selectedPath;
      const classes = ['graph-node', selected ? 'selected' : '', item.cycle ? 'cycle' : '', item.broken ? 'broken' : '', item.ambiguous ? 'ambiguous' : ''].filter(Boolean).join(' ');
      const accessibleLabel = `${item.id}: ${item.title || strings.unknownTitle}. ${strings.status}: ${item.status || strings.unknownStatus}.${problemLabels.length ? ` ${problemLabels.join(', ')}.` : ''}`;
      const node = svgElement('g', { class: classes, transform: `translate(${item.x} ${item.y})`, tabindex: selected || (!selectedPath && index === 0) ? '0' : '-1', role: 'button', 'aria-label': accessibleLabel, 'aria-pressed': selected ? 'true' : 'false', 'aria-controls': 'graph-details', 'data-path': item.path });
      node.append(svgElement('rect', { width: '220', height: '76', rx: '5' }), svgElement('text', { x: '12', y: '22', class: 'node-id' }, item.id), svgElement('text', { x: '12', y: '43', class: 'node-title' }, truncate(item.title || strings.unknownTitle, 31)), svgElement('text', { x: '12', y: '63', class: 'node-status' }, truncate(item.status || strings.unknownStatus, 33)), svgElement('title', {}, `${item.id}\n${item.title || strings.unknownTitle}\n${item.path}`));
      const select = () => {
        selectedPath = item.path;
        requestedFocusPath = item.path;
        render(currentMessage);
        announce(format(strings.selectedAnnouncement, item.id, item.title || strings.unknownTitle));
      };
      node.addEventListener('click', select);
      node.addEventListener('keydown', (event) => navigateNodes(event, nodeElements, index, select));
      nodeElements.push(node); nodes.append(node);
    });
    svg.append(nodes); viewport.append(svg); return viewport;
  }
  function navigateNodes(event, nodes, index, select) {
    if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); select(); return; }
    let target = index;
    if (event.key === 'ArrowRight' || event.key === 'ArrowDown') target = Math.min(nodes.length - 1, index + 1);
    else if (event.key === 'ArrowLeft' || event.key === 'ArrowUp') target = Math.max(0, index - 1);
    else if (event.key === 'Home') target = 0;
    else if (event.key === 'End') target = nodes.length - 1;
    else return;
    event.preventDefault();
    nodes.forEach((node, nodeIndex) => node.setAttribute('tabindex', nodeIndex === target ? '0' : '-1'));
    nodes[target]?.focus();
  }
  function detail(layout) {
    const item = layout.nodes.find((node) => node.path === selectedPath);
    const aside = element('aside', { id: 'graph-details', class: 'details', 'aria-label': strings.details });
    aside.append(element('h2', {}, strings.details));
    if (!item) { aside.append(element('p', { class: 'select-hint' }, strings.selectHint)); return aside; }
    aside.append(element('h3', {}, `${item.id} · ${item.title || strings.unknownTitle}`));
    const values = element('dl');
    [[strings.status, item.status || strings.unknownStatus], [strings.format, item.format], [strings.path, item.path]].forEach(([label, value]) => { const row = element('div'); row.append(element('dt', {}, label), element('dd', {}, value)); values.append(row); });
    aside.append(values);
    const badges = element('div', { class: 'diagnostic-badges' });
    if (item.cycle) badges.append(element('span', { class: 'cycle' }, strings.cycle));
    if (item.broken) badges.append(element('span', { class: 'broken' }, strings.broken));
    if (item.ambiguous) badges.append(element('span', { class: 'ambiguous' }, strings.ambiguous));
    if (badges.childNodes.length) aside.append(badges);
    const actions = element('div', { class: 'detail-actions' });
    const open = element('button', { id: 'graph-open-adr', type: 'button' }, strings.openAdr); open.addEventListener('click', () => vscode.postMessage({ type: 'openAdr', path: item.path }));
    const isFocused = currentMessage.filters.focusPath === item.path;
    const focus = element('button', { id: 'graph-focus', type: 'button', class: 'secondary' }, isFocused ? strings.clearFocus : strings.focus);
    focus.addEventListener('click', () => vscode.postMessage({ type: 'focus', path: isFocused ? null : item.path }));
    actions.append(open, focus); aside.append(actions);
    return aside;
  }
  function diagnostics(items) {
    if (!items.length) return undefined;
    const section = element('section', { class: 'diagnostics' }); section.append(element('h2', {}, strings.diagnostics));
    const list = element('ul'); items.forEach((item) => { const location = item.line ? `:${item.line}${item.column ? `:${item.column}` : ''}` : ''; const entry = element('li', { class: item.severity }); entry.append(element('strong', {}, item.code), document.createTextNode(` · ${item.path}${location} · ${item.message}`)); list.append(entry); }); section.append(list); return section;
  }
  function render(message, loadingFocus) {
    currentMessage = message;
    const activeId = document.activeElement?.id;
    const activePath = document.activeElement?.getAttribute('data-path');
    if (selectedPath && !message.layout.nodes.some((node) => node.path === selectedPath)) selectedPath = undefined;
    app.replaceChildren(); app.append(header(message), refreshNote(), metrics(message.summary), filters(message));
    if (message.summary.nodes === 0) {
      app.append(element('section', { class: 'state compact', role: 'status' }, strings.empty));
      restoreFocus(loadingFocus?.path, loadingFocus?.id || activeId);
      requestedFocusPath = undefined;
      return;
    }
    app.append(legend());
    if (message.filters.focusPath) app.append(element('p', { class: 'notice' }, strings.focused));
    if (message.summary.relationships === 0) app.append(element('p', { class: 'notice' }, strings.noRelationships));
    if (message.layout.totalMatchingNodes === 0) app.append(element('p', { class: 'notice' }, strings.noMatches));
    if (message.layout.truncated) app.append(element('p', { class: 'notice warning' }, `${format(strings.visibleNodes, message.layout.nodes.length, message.layout.totalMatchingNodes)} ${format(strings.visualLimit, message.visibleLimit)}`));
    const help = element('p', { id: 'graph-keyboard-help', class: 'keyboard-help' }, strings.keyboardHelp); app.append(help);
    const content = element('div', { class: 'graph-content' }); content.append(graph(message.layout), detail(message.layout)); app.append(content);
    const diagnosticSection = diagnostics(message.diagnostics); if (diagnosticSection) app.append(diagnosticSection);
    restoreFocus(requestedFocusPath || loadingFocus?.path || activePath, loadingFocus?.id || activeId);
    requestedFocusPath = undefined;
  }
  function restoreFocus(path, id) {
    const node = path && [...document.querySelectorAll('.graph-node')].find((candidate) => candidate.getAttribute('data-path') === path);
    const target = node || (id ? document.getElementById(id) : undefined);
    target?.focus();
  }
  function focusedElement() {
    return { id: document.activeElement?.id, path: document.activeElement?.getAttribute('data-path') };
  }
  function announce(message) {
    statusRegion.textContent = '';
    statusRegion.textContent = message;
  }
  function markerForType(type) {
    if (type === 'depends-on') return 'depends';
    if (type === 'supersedes') return 'supersedes';
    return 'superseded';
  }
  function labelForType(type) { return type === 'depends-on' ? strings.dependsOn : type === 'supersedes' ? strings.supersedes : strings.supersededBy; }
  function truncate(value, maximum) { return value.length > maximum ? `${value.slice(0, maximum - 1)}…` : value; }
  function format(template, ...values) { return values.reduce((result, value, index) => result.replaceAll(`{${index}}`, String(value)), template); }

  document.addEventListener('DOMContentLoaded', () => vscode.postMessage({ type: 'ready' }));
  document.addEventListener('focusin', () => { if (loading) focusChangedDuringLoading = true; });
  window.addEventListener('message', (event) => {
    const message = event.data;
    if (!message || typeof message !== 'object') return;
    if (message.strings && typeof message.strings === 'object') strings = message.strings;
    if (message.type === 'loading') {
      focusBeforeLoading = focusedElement();
      focusChangedDuringLoading = false;
      loading = true;
      state(strings.title || 'ADR Relationships', strings.loading || 'Loading…', false);
    } else if (message.type === 'graph') {
      const filtersChanged = currentMessage && JSON.stringify(currentMessage.filters) !== JSON.stringify(message.filters);
      const loadingFocus = loading && !focusChangedDuringLoading ? focusBeforeLoading : undefined;
      loading = false;
      focusBeforeLoading = undefined;
      render(message, loadingFocus);
      if (filtersChanged) announce(format(strings.filtersAnnouncement, message.layout.totalMatchingNodes));
    } else if (message.type === 'graphError') {
      loading = false;
      focusBeforeLoading = undefined;
      state(strings.title || 'ADR Relationships', String(message.message || 'Error'), true);
    }
  });
}());
