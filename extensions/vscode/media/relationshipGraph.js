/* global acquireVsCodeApi, document, window */
(function () {
  'use strict';
  const vscode = acquireVsCodeApi();
  const app = document.getElementById('app');
  const svgNamespace = 'http://www.w3.org/2000/svg';
  let strings = {};

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
  function action(label, type) {
    const button = element('button', { type: 'button' }, label);
    button.addEventListener('click', () => vscode.postMessage({ type }));
    return button;
  }
  function state(title, detail, retry) {
    app.replaceChildren();
    const section = element('section', { class: 'state', role: retry ? 'alert' : 'status' });
    section.append(element('h1', {}, title), element('p', {}, detail));
    if (retry) section.append(action(strings.retry || 'Retry', 'refresh'));
    app.append(section);
  }
  function header(message) {
    const node = element('header', { class: 'graph-header' });
    const heading = element('div');
    heading.append(element('h1', {}, strings.title), element('p', { class: 'directory' }, `${message.workspace} · ${message.directory}`));
    node.append(heading, action(strings.refresh, 'refresh'));
    return node;
  }
  function metrics(summary) {
    const node = element('dl', { class: 'metrics' });
    [[strings.nodes, summary.nodes], [strings.relationships, summary.relationships], [strings.resolved, summary.resolved], [strings.unresolved, summary.unresolved], [strings.ambiguous, summary.ambiguous]].forEach(([label, value]) => {
      const item = element('div'); item.append(element('dt', {}, label), element('dd', {}, value)); node.append(item);
    });
    return node;
  }
  function legend() {
    const node = element('section', { class: 'legend', 'aria-label': strings.relationships });
    [['depends-on', strings.dependsOn], ['supersedes', strings.supersedes], ['superseded-by', strings.supersededBy], ['unresolved', strings.unresolved], ['ambiguous', strings.ambiguous]].forEach(([kind, label]) => {
      const item = element('span'); item.append(element('i', { class: `legend-line ${kind}` }), document.createTextNode(label)); node.append(item);
    });
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
    const viewport = element('div', { class: 'graph-viewport', tabindex: '0' });
    const svg = svgElement('svg', { viewBox: `0 0 ${layout.width} ${layout.height}`, width: layout.width, height: layout.height, role: 'img', 'aria-label': strings.relationships });
    const defs = svgElement('defs'); addMarker(defs, 'arrow-depends', 'marker depends-on'); addMarker(defs, 'arrow-supersedes', 'marker supersedes'); addMarker(defs, 'arrow-superseded', 'marker superseded-by'); svg.append(defs);
    const edges = svgElement('g', { class: 'edges' });
    layout.edges.forEach((edge) => {
      const path = svgElement('path', { d: edgePath(edge), class: `edge ${edge.type} ${edge.resolution}`, 'marker-end': `url(#arrow-${edge.type === 'depends-on' ? 'depends' : edge.type === 'supersedes' ? 'supersedes' : 'superseded'})` });
      path.append(svgElement('title', {}, `${labelForType(edge.type)} · ${edge.resolution}: ${edge.declaredTarget}`)); edges.append(path);
      if (!edge.target) {
        edges.append(svgElement('circle', { cx: edge.source.x + 72, cy: edge.source.y, r: '7', class: 'unresolved-target' }));
        edges.append(svgElement('text', { x: edge.source.x + 72, y: edge.source.y + 4, class: 'unresolved-mark', 'text-anchor': 'middle' }, '!'));
      }
    });
    svg.append(edges);
    const nodes = svgElement('g', { class: 'nodes' });
    layout.nodes.forEach((item) => {
      const node = svgElement('g', { class: 'graph-node', transform: `translate(${item.x} ${item.y})`, tabindex: '0', role: 'button', 'aria-label': `${item.id}: ${item.title || strings.unknownTitle}` });
      node.append(svgElement('rect', { width: '220', height: '76', rx: '5' }), svgElement('text', { x: '12', y: '22', class: 'node-id' }, item.id), svgElement('text', { x: '12', y: '43', class: 'node-title' }, truncate(item.title || strings.unknownTitle, 31)), svgElement('text', { x: '12', y: '63', class: 'node-status' }, truncate(item.status || strings.unknownStatus, 33)), svgElement('title', {}, `${item.id}\n${item.title || strings.unknownTitle}\n${item.path}`));
      const open = () => vscode.postMessage({ type: 'openAdr', path: item.path });
      node.addEventListener('click', open); node.addEventListener('keydown', (event) => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); open(); } });
      nodes.append(node);
    });
    svg.append(nodes); viewport.append(svg); return viewport;
  }
  function diagnostics(items) {
    if (!items.length) return undefined;
    const section = element('section', { class: 'diagnostics' }); section.append(element('h2', {}, strings.diagnostics));
    const list = element('ul'); items.forEach((item) => { const location = item.line ? `:${item.line}${item.column ? `:${item.column}` : ''}` : ''; const entry = element('li', { class: item.severity }); entry.append(element('strong', {}, item.code), document.createTextNode(` · ${item.path}${location} · ${item.message}`)); list.append(entry); }); section.append(list); return section;
  }
  function render(message) {
    app.replaceChildren(); app.append(header(message), metrics(message.summary));
    if (message.summary.nodes === 0) { app.append(element('section', { class: 'state', role: 'status' }, strings.empty)); return; }
    app.append(legend());
    if (message.summary.relationships === 0) app.append(element('p', { class: 'notice', role: 'status' }, strings.noRelationships));
    app.append(graph(message.layout));
    const diagnosticSection = diagnostics(message.diagnostics); if (diagnosticSection) app.append(diagnosticSection);
  }
  function labelForType(type) { return type === 'depends-on' ? strings.dependsOn : type === 'supersedes' ? strings.supersedes : strings.supersededBy; }
  function truncate(value, maximum) { return value.length > maximum ? `${value.slice(0, maximum - 1)}…` : value; }

  document.addEventListener('DOMContentLoaded', () => vscode.postMessage({ type: 'ready' }));
  window.addEventListener('message', (event) => {
    const message = event.data;
    if (!message || typeof message !== 'object') return;
    if (message.strings && typeof message.strings === 'object') strings = message.strings;
    if (message.type === 'loading') state(strings.title || 'ADR Relationships', strings.loading || 'Loading…', false);
    else if (message.type === 'graph') render(message);
    else if (message.type === 'graphError') state(strings.title || 'ADR Relationships', String(message.message || 'Error'), true);
  });
}());
