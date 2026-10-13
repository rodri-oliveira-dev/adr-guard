/* global acquireVsCodeApi, confirm, document, window, clearTimeout, setTimeout */
(function () {
  'use strict';
  const vscode = acquireVsCodeApi();
  const app = document.getElementById('app');
  let strings = { 'en-US': {}, 'pt-BR': {} };
  let interfaceLanguage;
  let workspaces = [];
  let trusted = true;
  let selectedWorkspace;
  let directory = '';
  let entries = [];
  let schema;
  let previewTimer;
  let templateTimer;
  let previewSequence = 0;
  let busyCreate = false;
  let formDirty = false;
  let createdPath;
  let highlightKind = 'created';
  let editDocument;
  const valuesByField = {};
  const restored = vscode.getState() || {};
  let listState = normalizeListState(restored.listState);

  function normalizeListState(value) {
    const allowedSorts = ['number-desc', 'number-asc', 'title-asc', 'date-desc'];
    return {
      search: typeof value?.search === 'string' ? value.search.slice(0, 512) : '',
      status: typeof value?.status === 'string' ? value.status.slice(0, 200) : '',
      sort: allowedSorts.includes(value?.sort) ? value.sort : 'number-desc',
      scrollTop: typeof value?.scrollTop === 'number' && Number.isFinite(value.scrollTop)
        ? Math.min(Math.max(value.scrollTop, 0), 10_000_000)
        : 0,
    };
  }

  function t(value, ...args) {
    let translated = strings[interfaceLanguage]?.[value] || strings['en-US']?.[value] || value;
    args.forEach((arg, index) => { translated = translated.replace(`{${index}}`, String(arg)); });
    return translated;
  }
  function element(name, attributes, text) {
    const node = document.createElement(name);
    for (const [key, value] of Object.entries(attributes || {})) {
      if (key === 'className') node.className = value;
      else node.setAttribute(key, value);
    }
    if (text !== undefined) node.textContent = text;
    return node;
  }
  function button(label, action, secondary) {
    const node = element('button', { type: 'button', ...(secondary ? { className: 'secondary' } : {}) }, label);
    node.addEventListener('click', action);
    return node;
  }
  function select(id, options) {
    const control = element('select', { id });
    options.forEach((option) => control.append(element('option', { value: option.value }, option.label)));
    return control;
  }
  function renderMessage(title, detail, action) {
    app.replaceChildren();
    const state = element('section', { className: 'empty-state', role: 'status' });
    state.append(element('h1', {}, title));
    if (detail) state.append(element('p', { className: 'intro' }, detail));
    if (action) state.append(action);
    app.append(state);
  }
  function persistListState() {
    listState.scrollTop = window.scrollY;
    persistState();
  }
  function persistState() { vscode.setState({ listState, selectedWorkspace, interfaceLanguage }); }
  function listMessage(type) { return { type, workspace: selectedWorkspace, state: listState }; }

  function renderList() {
    app.replaceChildren();
    const header = element('header', { className: 'list-header' });
    const heading = element('div');
    heading.append(element('h1', {}, t('Architecture Decision Records')), element('p', { className: 'intro directory' }, directory));
    const actions = element('div', { className: 'actions compact' });
    const language = element('div', { className: 'language-switch', role: 'group', 'aria-label': t('Interface language') });
    [['en-US', 'English'], ['pt-BR', 'Português (Brasil)']].forEach(([value, label]) => {
      const choice = button(label, () => { persistListState(); interfaceLanguage = value; persistState(); renderList(); }, true);
      choice.setAttribute('aria-pressed', String(interfaceLanguage === value));
      language.append(choice);
    });
    actions.append(language, button(t('New ADR'), () => { persistListState(); vscode.postMessage(listMessage('newAdr')); }), button(t('Refresh'), () => vscode.postMessage(listMessage('refreshList')), true));
    header.append(heading, actions);
    const controls = element('section', { className: 'filters', 'aria-label': t('Architecture Decision Records') });
    const workspace = select('list-workspace', workspaces.map((item) => ({ value: item.id, label: item.name })));
    workspace.value = selectedWorkspace;
    workspace.addEventListener('change', () => { persistListState(); selectedWorkspace = workspace.value; vscode.postMessage(listMessage('selectWorkspace')); });
    const search = element('input', { id: 'search', type: 'search', placeholder: t('Search ADRs'), 'aria-label': t('Search ADRs'), autocomplete: 'off' });
    search.value = listState.search;
    const statuses = [...new Set(entries.map((entry) => entry.status).filter(Boolean))].sort((a, b) => a.localeCompare(b, undefined, { sensitivity: 'base' }));
    if (listState.status && !statuses.includes(listState.status)) statuses.push(listState.status);
    const status = select('status-filter', [{ value: '', label: t('All statuses') }, ...statuses.map((value) => ({ value, label: value }))]);
    status.value = listState.status;
    status.setAttribute('aria-label', t('Filter by status'));
    const sort = select('sort', [
      { value: 'number-desc', label: t('Number (descending)') }, { value: 'number-asc', label: t('Number (ascending)') },
      { value: 'title-asc', label: t('Title (A–Z)') }, { value: 'date-desc', label: t('Date (newest)') },
    ]);
    sort.value = listState.sort;
    sort.setAttribute('aria-label', t('Sort by'));
    controls.append(workspace, search, status, sort);
    const announcement = element('p', { id: 'list-count', className: 'count', role: 'status', 'aria-live': 'polite' });
    const banner = element('div', { id: 'created-banner', className: 'notice hidden', role: 'status' });
    const list = element('div', { id: 'adr-list', className: 'adr-list' });
    app.append(header, controls, banner, announcement, list);
    const update = () => renderEntries(list, announcement, banner);
    search.addEventListener('input', () => { listState.search = search.value; update(); });
    status.addEventListener('change', () => { listState.status = status.value; update(); });
    sort.addEventListener('change', () => { listState.sort = sort.value; update(); });
    const targetScroll = listState.scrollTop || 0;
    update();
    window.requestAnimationFrame(() => { window.scrollTo(0, targetScroll); listState.scrollTop = targetScroll; });
  }

  function renderEntries(list, announcement, banner) {
    const needle = listState.search.trim().toLocaleLowerCase();
    const filtered = entries.filter((entry) => (!listState.status || entry.status === listState.status)
      && (!needle || [entry.title, entry.id, entry.relativePath].filter(Boolean).join('\0').toLocaleLowerCase().includes(needle)))
      .sort(compareEntries);
    announcement.textContent = filtered.length === entries.length ? t('{0} ADRs shown', filtered.length) : t('{0} of {1} ADRs shown', filtered.length, entries.length);
    list.replaceChildren();
    if (!entries.length) {
      const empty = element('section', { className: 'empty-state' });
      empty.append(element('h2', {}, t('No ADRs were found in this directory.')), element('p', { className: 'intro' }, t('Create your first ADR to record an architectural decision.')), button(t('New ADR'), () => vscode.postMessage(listMessage('newAdr'))));
      list.append(empty);
    } else if (!filtered.length) {
      const empty = element('section', { className: 'empty-state' });
      empty.append(element('h2', {}, t('No ADRs match the current search and filters.')), button(t('Clear filters'), clearFilters, true));
      list.append(empty);
    } else {
      filtered.forEach((entry) => list.append(renderCard(entry)));
    }
    banner.replaceChildren();
    banner.classList.add('hidden');
    if (createdPath) {
      const visible = filtered.some((entry) => entry.relativePath === createdPath);
      banner.classList.remove('hidden');
      banner.append(element('span', {}, visible
        ? t(highlightKind === 'updated' ? 'ADR updated.' : 'ADR created.')
        : t(highlightKind === 'updated' ? 'The updated ADR is hidden by the current filters.' : 'The new ADR is hidden by the current filters.')));
      if (!visible) banner.append(button(t('Reveal it'), () => vscode.postMessage({ type: 'revealCreated', workspace: selectedWorkspace }), true));
    }
    persistState();
  }

  function clearFilters() { listState.search = ''; listState.status = ''; renderList(); }
  function compareEntries(left, right) {
    const fallback = left.relativePath.localeCompare(right.relativePath, 'en', { sensitivity: 'case' });
    if (listState.sort === 'title-asc') return optionalCompare(left.title, right.title, false) || fallback;
    if (listState.sort === 'date-desc') return optionalCompare(left.date, right.date, true) || fallback;
    const number = numberCompare(left.id, right.id);
    return (listState.sort === 'number-desc' ? -number : number) || fallback;
  }
  function optionalCompare(left, right, descending) { if (left === undefined) return right === undefined ? 0 : 1; if (right === undefined) return -1; const result = left.localeCompare(right, undefined, { sensitivity: 'base' }); return descending ? -result : result; }
  function numberCompare(left, right) { if (left === undefined) return right === undefined ? 0 : 1; if (right === undefined) return -1; return BigInt(left) < BigInt(right) ? -1 : BigInt(left) > BigInt(right) ? 1 : 0; }
  function renderCard(entry) {
    const card = element('article', { className: `adr-card${entry.relativePath === createdPath ? ' created' : ''}`, ...(entry.relativePath === createdPath ? { tabindex: '-1' } : {}) });
    const identity = element('span', { className: 'identifier' }, entry.id || t('No identifier'));
    const title = element('h2', {}, entry.title || t('Unknown title'));
    const metadata = element('div', { className: 'metadata' });
    if (entry.status) metadata.append(element('span', { className: 'status' }, entry.status));
    if (entry.date) metadata.append(element('time', { datetime: entry.date }, entry.date));
    const file = element('p', { className: 'path' }, entry.relativePath);
    const cardActions = element('div', { className: 'actions compact' });
    const edit = button(t('Edit'), () => {
      persistListState();
      vscode.postMessage({ type: 'editAdr', workspace: selectedWorkspace, relativePath: entry.relativePath, state: listState });
    });
    const open = button(t('Open Markdown'), () => vscode.postMessage({ type: 'openAdr', workspace: selectedWorkspace, relativePath: entry.relativePath }), true);
    cardActions.append(edit, open);
    card.append(identity, title, metadata, file);
    if (entry.warning || !entry.title) card.append(element('p', { className: 'warning' }, t('This ADR has incomplete metadata.')));
    card.append(cardActions);
    return card;
  }

  function appendField(container, id, label, control, help) {
    const group = element('div', { className: 'field' });
    group.append(element('label', { for: id }, label), control);
    if (help) { const helpId = `${id}-help`; control.setAttribute('aria-describedby', helpId); group.append(element('p', { id: helpId, className: 'help' }, help)); }
    container.append(group);
  }
  function renderForm(initialCulture) {
    if (!trusted) return renderMessage(t('ADR Guard'), t('Trust this workspace to browse ADRs.'));
    if (!workspaces.length) return renderMessage(t('ADR Guard'), t('Open a local workspace folder to browse ADRs.'));
    clearCreationValues();
    app.replaceChildren();
    const back = button(`← ${t('Back to ADR list')}`, backToList, true);
    back.id = 'edit-back';
    const heading = element('h1', {}, t('Create ADR with Form'));
    const note = element('p', { className: 'intro' }, t('No file is written until you choose Create ADR.'));
    const statusNode = element('div', { id: 'status', role: 'status', 'aria-live': 'polite' });
    const error = element('div', { id: 'error', role: 'alert', className: 'error hidden' });
    const form = element('form', { id: 'adr-form', novalidate: '' });
    const workspace = select('workspace', workspaces.map((item) => ({ value: item.id, label: item.name })));
    workspace.value = selectedWorkspace || workspaces[0].id;
    selectedWorkspace = workspace.value;
    const directoryInput = element('input', { id: 'directory', type: 'text', required: '', autocomplete: 'off' });
    directoryInput.value = selectedWorkspaceItem().directory;
    const template = select('template', [{ value: 'configured', label: t('Configured template') }, { value: 'minimal', label: t('Minimal') }, { value: 'extended', label: t('Extended') }]);
    const templateHelp = element('p', { id: 'template-help', className: 'help' }); template.setAttribute('aria-describedby', 'template-help');
    const culture = select('culture', [{ value: 'en-US', label: 'English (United States)' }, { value: 'pt-BR', label: 'Português (Brasil)' }]); culture.value = initialCulture || 'en-US';
    const title = element('input', { id: 'title', type: 'text', required: '', maxlength: '512', autocomplete: 'off' });
    const fields = element('section', { id: 'content-fields', 'aria-label': t('Creation template') });
    const preview = element('pre', { id: 'preview', tabindex: '0' }); preview.append(element('code', {}, t('Preview unavailable until required fields are complete.')));
    const actions = element('div', { className: 'actions' });
    const submit = element('button', { id: 'create', type: 'submit' }, t('Create ADR'));
    const cancel = button(t('Cancel'), backToList, true);
    const traditional = button(t('Use traditional creation'), () => vscode.postMessage({ type: 'traditional' }), true); traditional.id = 'traditional'; traditional.classList.add('hidden');
    actions.append(submit, cancel, traditional);
    appendField(form, 'workspace', t('Workspace folder'), workspace); appendField(form, 'directory', t('ADR destination directory'), directoryInput);
    const templateGroup = element('div', { className: 'field' }); templateGroup.append(element('label', { for: 'template' }, t('Creation template')), template, templateHelp); form.append(templateGroup);
    appendField(form, 'culture', t('Document language'), culture); appendField(form, 'title', t('Decision title'), title);
    form.append(fields, element('h2', {}, t('Markdown preview')), preview, actions); app.append(back, heading, note, statusNode, error, form);
    workspace.addEventListener('change', () => { selectedWorkspace = workspace.value; directoryInput.value = selectedWorkspaceItem().directory; markDirty(); describe(); });
    [directoryInput, template, culture].forEach((control) => control.addEventListener('change', () => { markDirty(); describe(); }));
    title.addEventListener('input', () => { markDirty(); schedulePreview(); });
    form.addEventListener('submit', (event) => { event.preventDefault(); if (!busyCreate && form.reportValidity()) postForm('create'); });
    describe();
  }

  function renderEditForm() {
    if (!editDocument) return;
    formDirty = false;
    app.replaceChildren();
    const back = button(`← ${t('Back to ADR list')}`, backToList, true);
    const heading = element('h1', {}, t('Edit ADR'));
    const identity = element('p', { className: 'intro path' }, editDocument.relativePath);
    const statusNode = element('div', { id: 'status', role: 'status', 'aria-live': 'polite' });
    const error = element('div', { id: 'error', role: 'alert', className: 'error hidden' });
    const form = element('form', { id: 'adr-edit-form', novalidate: '' });
    const title = element('input', { id: 'edit-title', type: 'text', required: '', maxlength: '512', autocomplete: 'off' });
    title.value = editDocument.response.title;
    appendField(form, 'edit-title', t('Decision title'), title);
    editDocument.response.fields.forEach((field) => {
      const id = `edit-${field.id}`;
      let control;
      if (field.kind === 'status' && Array.isArray(field.options) && field.options.length) {
        const options = field.options.map((value) => ({ value, label: value }));
        if (!field.options.includes(field.value)) options.unshift({ value: field.value, label: field.value });
        control = select(id, options);
        control.value = field.value;
      } else if (field.kind === 'metadata') {
        control = element('input', { id, type: 'text', maxlength: '32768', autocomplete: 'off' });
        control.value = field.value;
      } else {
        control = element('textarea', { id, rows: '7', maxlength: '32768' });
        control.value = field.value;
      }
      control.dataset.editFieldId = field.id;
      if (field.required) control.required = true;
      control.addEventListener('input', markDirty);
      control.addEventListener('change', markDirty);
      appendField(form, id, `${field.label} — ${field.required ? t('Required') : t('Optional')}`, control);
    });
    title.addEventListener('input', markDirty);
    const actions = element('div', { className: 'actions' });
    const save = element('button', { id: 'save-edit', type: 'submit' }, t('Save changes'));
    const cancel = button(t('Cancel'), backToList, true);
    cancel.id = 'edit-cancel';
    actions.append(save, cancel);
    form.append(actions);
    form.addEventListener('submit', (event) => {
      event.preventDefault();
      if (busyCreate || !form.reportValidity()) return;
      const fields = {};
      document.querySelectorAll('[data-edit-field-id]').forEach((control) => { fields[control.dataset.editFieldId] = control.value; });
      vscode.postMessage({
        type: 'saveEdit',
        editValues: {
          workspace: editDocument.workspace,
          relativePath: editDocument.relativePath,
          fingerprint: editDocument.response.fingerprint,
          title: title.value,
          fields,
        },
      });
    });
    app.append(back, heading, identity, statusNode, error, form);
    title.focus();
  }
  function selectedWorkspaceItem() {
    const workspaceControl = document.getElementById('workspace');
    const workspaceId = workspaceControl ? workspaceControl.value : selectedWorkspace;
    return workspaces.find((item) => item.id === workspaceId) || workspaces[0];
  }
  function currentValues() { document.querySelectorAll('[data-field-id]').forEach((control) => { valuesByField[control.dataset.fieldId] = control.value; }); return { workspace: document.getElementById('workspace').value, directory: document.getElementById('directory').value, template: document.getElementById('template').value, culture: document.getElementById('culture').value, title: document.getElementById('title').value, fields: Object.fromEntries((schema?.fields || []).map((field) => [field.id, valuesByField[field.id] || ''])) }; }
  function postForm(type) { vscode.postMessage({ type, values: currentValues() }); }
  function markDirty() { formDirty = true; }
  function clearCreationValues() { Object.keys(valuesByField).forEach((key) => { delete valuesByField[key]; }); }
  function backToList() { if (busyCreate || (formDirty && !confirm(t('Discard the unsaved form data and return to the ADR list?')))) return; vscode.postMessage({ type: 'cancel' }); }
  function describe() { schema = undefined; clearTimeout(previewTimer); clearTimeout(templateTimer); document.getElementById('status').textContent = t('Loading template…'); document.getElementById('content-fields').replaceChildren(); hideError(); postForm('describe'); templateTimer = setTimeout(() => { if (schema || !document.getElementById('adr-form')) return; const error = document.getElementById('error'); error.textContent = t('The template could not be displayed. Try again.'); error.classList.remove('hidden'); document.getElementById('status').textContent = ''; }, 5000); }
  function fieldLabel(id) { return t(({ context: 'Context', decisionDrivers: 'Decision drivers', optionsConsidered: 'Options considered', decision: 'Decision', rationale: 'Rationale', consequences: 'Consequences', positiveConsequences: 'Positive consequences', negativeConsequences: 'Negative consequences', risks: 'Risks', references: 'References' })[id] || id); }
  function fieldHelp(id) { return t(({ context: 'Describe the problem, constraints, and relevant stakeholders.', decisionDrivers: 'List requirements, quality attributes, constraints, and trade-offs.', optionsConsidered: 'Compare viable alternatives, including keeping the current approach.', decision: 'State the proposed choice, its scope, and why it addresses the problem.', rationale: 'Explain why this option best satisfies the decision drivers.', consequences: 'Describe expected impact and trade-offs.', positiveConsequences: 'Record expected benefits and assumptions.', negativeConsequences: 'Record costs, limitations, and added complexity.', risks: 'Identify uncertainties, failure modes, mitigations, and owners.', references: 'Add links to evidence, related documents, or ADRs.' })[id] || ''); }
  function renderSchema(response) { clearTimeout(templateTimer); schema = response.template; const fields = document.getElementById('content-fields'); const traditional = document.getElementById('traditional'); const create = document.getElementById('create'); const help = document.getElementById('template-help'); const status = document.getElementById('status'); if (!fields || !traditional || !create || !help || !status) { schema = undefined; return; } fields.replaceChildren(); if (!schema.compatible) { help.textContent = t('This custom template cannot be mapped safely to form fields. Use the traditional creation flow to preserve it.'); traditional.classList.remove('hidden'); create.disabled = true; status.textContent = ''; return; } traditional.classList.add('hidden'); create.disabled = false; help.textContent = schema.name === 'extended' ? t('Extended adds optional drivers, alternatives, rationale, risks, and references.') : t('Minimal captures the context, decision, and consequences.'); schema.fields.forEach((field) => { const id = `content-${field.id}`; const control = element('textarea', { id, rows: field.id === 'context' || field.id === 'decision' ? '7' : '5', 'data-field-id': field.id, maxlength: '32768', ...(field.required ? { required: '' } : {}) }); control.value = valuesByField[field.id] || ''; control.addEventListener('input', () => { valuesByField[field.id] = control.value; markDirty(); schedulePreview(); }); appendField(fields, id, `${fieldLabel(field.id)} — ${field.required ? t('Required') : t('Optional')}`, control, fieldHelp(field.id)); }); status.textContent = t('Preview updates after you edit the form.'); schedulePreview(); }
  function displayTemplate(response) { try { if (!document.getElementById('adr-form')) return; hideError(); renderSchema(response); } catch { clearTimeout(templateTimer); schema = undefined; const error = document.getElementById('error'); if (error) { error.textContent = t('The template could not be displayed. Try again.'); error.classList.remove('hidden'); } const status = document.getElementById('status'); if (status) status.textContent = ''; } }
  function completeForPreview() { return schema?.compatible && document.getElementById('title').value.trim() && schema.fields.every((field) => !field.required || (valuesByField[field.id] || '').trim()); }
  function schedulePreview() { clearTimeout(previewTimer); if (!completeForPreview()) { document.querySelector('#preview code').textContent = t('Preview unavailable until required fields are complete.'); return; } const sequence = ++previewSequence; previewTimer = setTimeout(() => { if (sequence === previewSequence) postForm('preview'); }, 450); }
  function setCreateBusy(active) { busyCreate = active; document.querySelectorAll('#adr-form input, #adr-form select, #adr-form textarea, #create, #traditional').forEach((control) => { control.disabled = active; }); if (!active) document.getElementById('create').disabled = !schema?.compatible; }
  function hideError() { const error = document.getElementById('error'); if (error) { error.textContent = ''; error.classList.add('hidden'); } }

  window.addEventListener('message', (event) => {
    const message = event.data; if (!message || typeof message.type !== 'string') return;
    if (message.type === 'initialize') { strings = message.strings || strings; interfaceLanguage = restored.interfaceLanguage === 'pt-BR' || restored.interfaceLanguage === 'en-US' ? restored.interfaceLanguage : message.initialInterfaceLanguage; workspaces = Array.isArray(message.workspaces) ? message.workspaces : []; trusted = message.trusted !== false; selectedWorkspace = restored.selectedWorkspace && workspaces.some((item) => item.id === restored.selectedWorkspace) ? restored.selectedWorkspace : message.initialWorkspace; if (!trusted) renderMessage(t('ADR Guard'), t('Trust this workspace to browse ADRs.')); else if (!workspaces.length) renderMessage(t('ADR Guard'), t('Open a local workspace folder to browse ADRs.')); else if (message.initialScreen === 'form') renderForm(message.initialCulture); else renderMessage(t('Architecture Decision Records'), t('Loading ADRs…')); return; }
    if (message.type === 'showForm') { if (message.workspace) selectedWorkspace = message.workspace; renderForm(); return; }
    if (message.type === 'showList') { if (message.state) listState = message.state; clearCreationValues(); formDirty = false; editDocument = undefined; renderMessage(t('Architecture Decision Records'), t('Loading ADRs…')); return; }
    if (message.type === 'requestShowList') { backToList(); return; }
    if (message.type === 'requestShowForm') { if (formDirty && !confirm(t('Discard the unsaved form data and start a new ADR?'))) return; formDirty = false; vscode.postMessage(listMessage('newAdr')); return; }
    if (message.type === 'editLoading') { renderMessage(t('Edit ADR'), t('Loading ADR…')); return; }
    if (message.type === 'editLoaded') { selectedWorkspace = message.workspace; editDocument = message; renderEditForm(); return; }
    if (message.type === 'listLoading') { if (!document.getElementById('adr-list')) renderMessage(t('Architecture Decision Records'), t('Loading ADRs…')); return; }
    if (message.type === 'listResult') { if (message.workspace !== selectedWorkspace) return; directory = `${workspaces.find((item) => item.id === selectedWorkspace)?.name || ''} · ${message.directory}`; entries = Array.isArray(message.entries) ? message.entries : []; if (message.state) listState = message.state; createdPath = message.createdPath; highlightKind = message.highlightKind || 'created'; renderList(); return; }
    if (message.type === 'listError') { const missing = message.kind === 'missing-directory'; renderMessage(t('Architecture Decision Records'), missing ? t('The configured ADR directory does not exist.') : t('The ADR directory could not be read.'), button(t('Retry'), () => vscode.postMessage(listMessage('refreshList')))); if (missing) app.append(element('p', { className: 'intro centered' }, t('ADR Guard does not create directories while browsing.'))); return; }
    if (message.type === 'clearListFilters') { listState.search = ''; listState.status = ''; return; }
    if (message.type === 'created') { clearCreationValues(); formDirty = false; createdPath = message.relativePath; renderMessage(t('Architecture Decision Records'), t('Loading ADRs…')); return; }
    if (message.type === 'edited') { formDirty = false; createdPath = message.relativePath; highlightKind = 'updated'; editDocument = undefined; renderMessage(t('Architecture Decision Records'), t('Loading ADRs…')); return; }
    if (message.type === 'templateLoaded') { displayTemplate(message.response); return; }
    if (message.type === 'result') { if (!document.getElementById('adr-form')) return; hideError(); if (message.response.operation === 'preview') document.querySelector('#preview code').textContent = message.response.markdown || ''; return; }
    if (message.type === 'error' || message.type === 'listNavigationError' || message.type === 'editError') { const error = document.getElementById('error'); if (error) { error.textContent = message.message || 'Error'; error.classList.remove('hidden'); if (message.traditionalAvailable) document.getElementById('traditional')?.classList.remove('hidden'); } else if (message.type === 'editError') { renderMessage(t('Edit ADR'), message.message || 'Error', button(t('Back to ADR list'), backToList, true)); } else window.alert(message.message || 'Error'); return; }
    if (message.type === 'busy' && message.operation === 'create') { setCreateBusy(true); document.getElementById('status').textContent = t('Creating ADR…'); }
    if (message.type === 'idle' && message.operation === 'create') setCreateBusy(false);
    if (message.type === 'editBusy') { busyCreate = true; document.querySelectorAll('#adr-edit-form input, #adr-edit-form select, #adr-edit-form textarea, #adr-edit-form button, #edit-back').forEach((control) => { control.disabled = true; }); document.getElementById('status').textContent = t('Saving changes…'); }
    if (message.type === 'editIdle') { busyCreate = false; document.querySelectorAll('#adr-edit-form input, #adr-edit-form select, #adr-edit-form textarea, #adr-edit-form button, #edit-back').forEach((control) => { control.disabled = false; }); }
  });
  renderMessage('', '');
  vscode.postMessage({ type: 'ready' });
}());
