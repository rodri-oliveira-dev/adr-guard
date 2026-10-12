/* global acquireVsCodeApi, document, window, clearTimeout, setTimeout */
(function () {
  'use strict';
  const vscode = acquireVsCodeApi();
  const app = document.getElementById('app');
  let strings = {};
  let workspaces = [];
  let schema;
  let previewTimer;
  let previewSequence = 0;
  let busyCreate = false;
  const valuesByField = {};

  function t(value) { return strings[value] || value; }
  function element(name, attributes, text) {
    const node = document.createElement(name);
    for (const [key, value] of Object.entries(attributes || {})) {
      if (key === 'className') node.className = value;
      else node.setAttribute(key, value);
    }
    if (text !== undefined) node.textContent = text;
    return node;
  }
  function appendField(container, id, label, control, help) {
    const group = element('div', { className: 'field' });
    const labelNode = element('label', { for: id }, label);
    group.append(labelNode, control);
    if (help) {
      const helpId = `${id}-help`;
      control.setAttribute('aria-describedby', helpId);
      group.append(element('p', { id: helpId, className: 'help' }, help));
    }
    container.append(group);
  }
  function select(id, options) {
    const control = element('select', { id });
    for (const option of options) {
      const node = element('option', { value: option.value }, option.label);
      control.append(node);
    }
    return control;
  }
  function renderShell(message) {
    app.replaceChildren();
    app.append(element('p', { className: 'loading' }, message));
  }
  function renderForm(initialWorkspace, initialCulture) {
    app.replaceChildren();
    const heading = element('h1', {}, t('Create ADR with Form'));
    const note = element('p', { className: 'intro' }, t('No file is written until you choose Create ADR.'));
    const status = element('div', { id: 'status', role: 'status', 'aria-live': 'polite' });
    const error = element('div', { id: 'error', role: 'alert', className: 'error hidden' });
    const form = element('form', { id: 'adr-form', novalidate: '' });
    const workspace = select('workspace', workspaces.map((item) => ({ value: item.id, label: item.name })));
    workspace.value = initialWorkspace || workspaces[0].id;
    const directory = element('input', { id: 'directory', type: 'text', required: '', autocomplete: 'off' });
    directory.value = selectedWorkspace().directory;
    const template = select('template', [
      { value: 'configured', label: t('Configured template') },
      { value: 'minimal', label: t('Minimal') },
      { value: 'extended', label: t('Extended') },
    ]);
    const templateHelp = element('p', { id: 'template-help', className: 'help' });
    template.setAttribute('aria-describedby', 'template-help');
    const culture = select('culture', [
      { value: 'en-US', label: 'English (United States)' },
      { value: 'pt-BR', label: 'Português (Brasil)' },
    ]);
    culture.value = initialCulture;
    const title = element('input', { id: 'title', type: 'text', required: '', maxlength: '512', autocomplete: 'off' });
    const fields = element('section', { id: 'content-fields', 'aria-label': t('Creation template') });
    const previewHeading = element('h2', {}, t('Markdown preview'));
    const preview = element('pre', { id: 'preview', tabindex: '0' });
    preview.append(element('code', {}, t('Preview unavailable until required fields are complete.')));
    const actions = element('div', { className: 'actions' });
    const submit = element('button', { id: 'create', type: 'submit' }, t('Create ADR'));
    const cancel = element('button', { id: 'cancel', type: 'button', className: 'secondary' }, t('Cancel'));
    const traditional = element('button', { id: 'traditional', type: 'button', className: 'secondary hidden' }, t('Use traditional creation'));
    actions.append(submit, cancel, traditional);
    appendField(form, 'workspace', t('Workspace folder'), workspace);
    appendField(form, 'directory', t('ADR destination directory'), directory);
    const templateGroup = element('div', { className: 'field' });
    templateGroup.append(element('label', { for: 'template' }, t('Creation template')), template, templateHelp);
    form.append(templateGroup);
    appendField(form, 'culture', t('Document language'), culture);
    appendField(form, 'title', t('Decision title'), title);
    form.append(fields, previewHeading, preview, actions);
    app.append(heading, note, status, error, form);

    workspace.addEventListener('change', () => {
      directory.value = selectedWorkspace().directory;
      describe();
    });
    for (const control of [directory, template, culture]) control.addEventListener('change', describe);
    title.addEventListener('input', schedulePreview);
    form.addEventListener('submit', (event) => {
      event.preventDefault();
      if (busyCreate || !form.reportValidity()) return;
      post('create');
    });
    cancel.addEventListener('click', () => vscode.postMessage({ type: 'cancel' }));
    traditional.addEventListener('click', () => vscode.postMessage({ type: 'traditional' }));
    describe();
  }
  function selectedWorkspace() {
    const id = document.getElementById('workspace').value;
    return workspaces.find((item) => item.id === id) || workspaces[0];
  }
  function currentValues() {
    document.querySelectorAll('[data-field-id]').forEach((control) => {
      valuesByField[control.dataset.fieldId] = control.value;
    });
    return {
      workspace: document.getElementById('workspace').value,
      directory: document.getElementById('directory').value,
      template: document.getElementById('template').value,
      culture: document.getElementById('culture').value,
      title: document.getElementById('title').value,
      fields: Object.fromEntries((schema?.fields || []).map((field) => [field.id, valuesByField[field.id] || ''])),
    };
  }
  function post(type) { vscode.postMessage({ type, values: currentValues() }); }
  function describe() {
    schema = undefined;
    clearTimeout(previewTimer);
    document.getElementById('status').textContent = t('Loading template…');
    document.getElementById('content-fields').replaceChildren();
    post('describe');
  }
  function fieldLabel(id) {
    const labels = {
      context: 'Context', decisionDrivers: 'Decision drivers', optionsConsidered: 'Options considered',
      decision: 'Decision', rationale: 'Rationale', consequences: 'Consequences',
      positiveConsequences: 'Positive consequences', negativeConsequences: 'Negative consequences',
      risks: 'Risks', references: 'References',
    };
    return t(labels[id] || id);
  }
  function fieldHelp(id) {
    const helps = {
      context: 'Describe the problem, constraints, and relevant stakeholders.',
      decisionDrivers: 'List requirements, quality attributes, constraints, and trade-offs.',
      optionsConsidered: 'Compare viable alternatives, including keeping the current approach.',
      decision: 'State the proposed choice, its scope, and why it addresses the problem.',
      rationale: 'Explain why this option best satisfies the decision drivers.',
      consequences: 'Describe expected impact and trade-offs.',
      positiveConsequences: 'Record expected benefits and assumptions.',
      negativeConsequences: 'Record costs, limitations, and added complexity.',
      risks: 'Identify uncertainties, failure modes, mitigations, and owners.',
      references: 'Add links to evidence, related documents, or ADRs.',
    };
    return t(helps[id] || '');
  }
  function renderSchema(response) {
    schema = response.template;
    const fields = document.getElementById('content-fields');
    fields.replaceChildren();
    const traditional = document.getElementById('traditional');
    const create = document.getElementById('create');
    const templateHelp = document.getElementById('template-help');
    if (!schema.compatible) {
      templateHelp.textContent = t('This custom template cannot be mapped safely to form fields. Use the traditional creation flow to preserve it.');
      traditional.classList.remove('hidden');
      create.disabled = true;
      document.getElementById('status').textContent = '';
      return;
    }
    traditional.classList.add('hidden');
    create.disabled = false;
    templateHelp.textContent = schema.name === 'extended'
      ? t('Extended adds optional drivers, alternatives, rationale, risks, and references.')
      : t('Minimal captures the context, decision, and consequences.');
    for (const field of schema.fields) {
      const id = `content-${field.id}`;
      const control = element('textarea', {
        id, rows: field.id === 'context' || field.id === 'decision' ? '7' : '5',
        'data-field-id': field.id, maxlength: '32768',
        ...(field.required ? { required: '' } : {}),
      });
      control.value = valuesByField[field.id] || '';
      control.addEventListener('input', () => {
        valuesByField[field.id] = control.value;
        schedulePreview();
      });
      const suffix = field.required ? t('Required') : t('Optional');
      appendField(fields, id, `${fieldLabel(field.id)} — ${suffix}`, control, fieldHelp(field.id));
    }
    document.getElementById('status').textContent = t('Preview updates after you edit the form.');
    schedulePreview();
  }
  function completeForPreview() {
    if (!schema || !schema.compatible || !document.getElementById('title').value.trim()) return false;
    return schema.fields.every((field) => !field.required || (valuesByField[field.id] || '').trim());
  }
  function schedulePreview() {
    clearTimeout(previewTimer);
    if (!completeForPreview()) {
      document.querySelector('#preview code').textContent = t('Preview unavailable until required fields are complete.');
      return;
    }
    const sequence = ++previewSequence;
    previewTimer = setTimeout(() => {
      if (sequence === previewSequence) post('preview');
    }, 450);
  }
  function setCreateBusy(active) {
    busyCreate = active;
    for (const control of document.querySelectorAll(
      '#adr-form input, #adr-form select, #adr-form textarea, #create, #traditional',
    )) control.disabled = active;
    if (!active) document.getElementById('create').disabled = !schema?.compatible;
  }
  window.addEventListener('message', (event) => {
    const message = event.data;
    if (!message || typeof message.type !== 'string') return;
    if (message.type === 'initialize') {
      strings = message.strings || {};
      workspaces = Array.isArray(message.workspaces) ? message.workspaces : [];
      if (workspaces.length) renderForm(message.initialWorkspace, message.initialCulture);
      return;
    }
    if (message.type === 'result') {
      hideError();
      if (message.response.operation === 'describe') renderSchema(message.response);
      if (message.response.operation === 'preview') {
        document.querySelector('#preview code').textContent = message.response.markdown || '';
      }
      return;
    }
    if (message.type === 'error') {
      const error = document.getElementById('error');
      error.textContent = message.message || 'Error';
      error.classList.remove('hidden');
      if (message.traditionalAvailable) document.getElementById('traditional')?.classList.remove('hidden');
      return;
    }
    if (message.type === 'busy') {
      if (message.operation === 'create') {
        setCreateBusy(true);
        document.getElementById('status').textContent = t('Creating ADR…');
      }
      return;
    }
    if (message.type === 'idle' && message.operation === 'create') {
      setCreateBusy(false);
    }
  });
  function hideError() {
    const error = document.getElementById('error');
    if (error) { error.textContent = ''; error.classList.add('hidden'); }
  }
  renderShell('');
  vscode.postMessage({ type: 'ready' });
}());
