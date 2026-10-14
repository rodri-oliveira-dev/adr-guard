/* global acquireVsCodeApi, confirm, document, window, clearTimeout, setTimeout */
(function () {
  'use strict';
  const vscode = acquireVsCodeApi();
  const app = document.getElementById('app');
  let strings = { 'en-US': {}, 'pt-BR': {} };
  let interfaceLanguage;
  let interfacePreference = 'auto';
  let editorInterfaceLanguage = 'en-US';
  let hostPlatform = 'linux';
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
  let settingsBaseline;
  let settingsDraft;
  let settingsDirty = false;
  let settingsBusy = false;
  let installationCheckBusy = false;
  let installationCheckResult;
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
  function persistState() { vscode.setState({ listState, selectedWorkspace }); }
  function listMessage(type) { return { type, workspace: selectedWorkspace, state: listState }; }
  function effectiveInterfaceLanguage(preference) {
    return preference === 'pt-BR' || preference === 'en-US' ? preference : editorInterfaceLanguage;
  }

  function renderListHeader(initializationState) {
    const header = element('header', { className: 'list-header' });
    const heading = element('div', { className: 'list-heading' });
    heading.append(element('h1', {}, t('Architecture Decision Records')), element('p', { className: 'intro directory' }, directory));
    const controls = element('div', { className: 'header-controls' });
    const actions = element('div', { className: 'header-actions', role: 'group', 'aria-label': t('Architecture Decision Records') });
    const initialize = button(t('Initialize repository'), () => {
      persistListState();
      vscode.postMessage(listMessage('initializeRepository'));
    }, true);
    let initializeControl = initialize;
    if (initializationState !== 'available') {
      const hint = t(initializationState === 'initialized'
        ? 'The ADR repository is already initialized.'
        : 'Repository initialization is unavailable until the ADR directory can be read.');
      initialize.disabled = true;
      initialize.setAttribute('aria-label', `${t('Initialize repository')}. ${hint}`);
      initializeControl = element('span', { className: 'disabled-button-wrapper', title: hint });
      initializeControl.append(initialize);
    }
    const check = button(t('Check installation'), () => vscode.postMessage(listMessage('checkInstallation')), true);
    const refresh = button(t('Refresh'), () => { persistListState(); vscode.postMessage(listMessage('refreshList')); }, true);
    const settings = button('⚙', () => { persistListState(); vscode.postMessage(listMessage('openSettings')); }, true);
    settings.id = 'open-settings';
    settings.classList.add('icon-button');
    settings.setAttribute('aria-label', t('Settings'));
    settings.setAttribute('title', t('Settings'));
    actions.append(refresh, check, initializeControl, button(t('New ADR'), () => { persistListState(); vscode.postMessage(listMessage('newAdr')); }), settings);
    controls.append(actions);
    header.append(heading, controls);
    return header;
  }

  function renderList() {
    app.replaceChildren();
    const header = renderListHeader(entries.length > 0 ? 'initialized' : 'available');
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
    list.removeAttribute('role');
    if (!entries.length) {
      const empty = element('section', { className: 'empty-state' });
      empty.append(element('h2', {}, t('No ADRs were found in this directory.')), element('p', { className: 'intro' }, t('Create your first ADR to record an architectural decision.')), button(t('New ADR'), () => vscode.postMessage(listMessage('newAdr'))));
      list.append(empty);
    } else if (!filtered.length) {
      const empty = element('section', { className: 'empty-state' });
      empty.append(element('h2', {}, t('No ADRs match the current search and filters.')), button(t('Clear filters'), clearFilters, true));
      list.append(empty);
    } else {
      list.setAttribute('role', 'list');
      filtered.forEach((entry) => list.append(renderRow(entry)));
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
  function renderListError(kind) {
    const missing = kind === 'missing-directory';
    app.replaceChildren(renderListHeader(missing ? 'available' : 'unavailable'));
    const state = element('section', { className: 'empty-state', role: 'status' });
    state.append(element('h2', {}, missing ? t('The configured ADR directory does not exist.') : t('The ADR directory could not be read.')));
    if (missing) state.append(element('p', { className: 'intro' }, t('ADR Guard does not create directories while browsing.')));
    state.append(button(t('Retry'), () => vscode.postMessage(listMessage('refreshList')), true));
    app.append(state);
  }
  function settingsValues() {
    return {
      workspace: document.getElementById('settings-workspace').value,
      uiLanguage: document.getElementById('ui-language').value,
      documentLanguage: document.getElementById('document-language').value,
      directory: document.getElementById('settings-directory').value,
      adrFormat: document.getElementById('adr-format').value,
      defaultTemplate: document.getElementById('default-template').value,
      onSave: document.getElementById('validation-on-save').checked,
      debounceMilliseconds: Number(document.getElementById('validation-debounce').value),
      baseline: document.getElementById('validation-baseline').value,
      baseReference: document.getElementById('validation-base-reference').value,
      cliPath: document.getElementById('cli-path').value,
      timeoutMilliseconds: Number(document.getElementById('cli-timeout').value),
      maxOutputBytes: Number(document.getElementById('cli-output-limit').value),
    };
  }
  function updateSettingsDraft() {
    settingsDraft = settingsValues();
    settingsDirty = JSON.stringify(settingsDraft) !== JSON.stringify(settingsBaseline);
  }
  function validSettingsDirectory(value) {
    const candidate = value.trim();
    return candidate.length > 0 && candidate.length <= 4096
      && !candidate.startsWith('/') && !candidate.startsWith('\\') && !/^[A-Za-z]:[\\/]/u.test(candidate)
      && !candidate.split(/[\\/]/u).includes('..');
  }
  function validBaseline(value) { return validSettingsDirectory(value) && value.trim().toLocaleLowerCase().endsWith('.json'); }
  function validGitReference(value) { const candidate = value.trim(); return candidate.length > 0 && candidate.length <= 256 && !candidate.startsWith('-') && !/[\s\p{Cc}]/u.test(candidate); }
  function validCliPath(value) {
    const candidate = value.trim();
    if (!candidate) return true;
    if (hostPlatform === 'win32') return /^[A-Za-z]:[\\/]/u.test(candidate) && !candidate.startsWith('\\\\');
    return candidate.startsWith('/');
  }
  function validInteger(value, minimum, maximum) { return Number.isInteger(value) && value >= minimum && value <= maximum; }
  function leaveSettings() {
    if (settingsBusy || installationCheckBusy) return;
    if (settingsDirty && !confirm(t('Discard the unsaved settings and return to the ADR list?'))) return;
    if (settingsBaseline) {
      interfacePreference = settingsBaseline.uiLanguage;
      interfaceLanguage = effectiveInterfaceLanguage(interfacePreference);
    }
    settingsDirty = false;
    vscode.postMessage(listMessage('cancelSettings'));
  }
  function setSettingsBusy(active) {
    settingsBusy = active;
    document.querySelectorAll('#settings-form input, #settings-form select, #settings-form button, .settings-back')
      .forEach((control) => { control.disabled = active; });
    const debounce = document.getElementById('validation-debounce');
    const onSave = document.getElementById('validation-on-save');
    if (!active && debounce && onSave) debounce.disabled = !onSave.checked;
    const status = document.getElementById('settings-status');
    if (status) status.textContent = active ? t('Saving settings…') : '';
  }
  function setInstallationCheckBusy(active) {
    installationCheckBusy = active;
    const control = document.getElementById('check-settings-installation');
    if (control) control.disabled = active || settingsBusy;
    document.querySelectorAll('#save-settings, .settings-back').forEach((item) => { item.disabled = active || settingsBusy; });
    const status = document.getElementById('installation-status');
    if (status && active) status.textContent = t('Checking installation…');
  }
  function renderSettings(values) {
    if (values) {
      settingsBaseline = { ...values };
      settingsDraft = { ...values };
      settingsDirty = false;
      selectedWorkspace = values.workspace;
      interfacePreference = values.uiLanguage;
      interfaceLanguage = effectiveInterfaceLanguage(interfacePreference);
      installationCheckResult = undefined;
    }
    if (!settingsDraft) return;
    app.replaceChildren();
    const back = button(`← ${t('Back to ADR list')}`, leaveSettings, true);
    back.id = 'settings-back';
    back.classList.add('form-back', 'settings-back');
    const headingGroup = element('div', { className: 'form-heading' });
    headingGroup.append(element('h1', {}, t('Settings')), element('p', { className: 'intro' }, t('Configure ADR Guard preferences.')));
    const header = element('header', { className: 'form-header' }); header.append(back, headingGroup);
    const navigation = sectionNavigation([
      { id: 'settings-languages', label: 'Languages' }, { id: 'settings-repository', label: 'Repository' },
      { id: 'settings-creation', label: 'Document creation' }, { id: 'settings-validation', label: 'Validation' },
      { id: 'settings-cli', label: 'CLI integration' },
    ]);
    const status = element('div', { id: 'settings-status', role: 'status', 'aria-live': 'polite' });
    const error = element('div', { id: 'error', role: 'alert', className: 'error hidden' });
    const form = element('form', { id: 'settings-form', novalidate: '' });
    const languages = element('fieldset', { id: 'settings-languages', className: 'form-section' }); languages.append(element('legend', {}, t('Languages')));
    const repository = element('fieldset', { id: 'settings-repository', className: 'form-section' }); repository.append(element('legend', {}, t('Repository')));
    const creation = element('fieldset', { id: 'settings-creation', className: 'form-section' }); creation.append(element('legend', {}, t('Document creation')));
    const validation = element('fieldset', { id: 'settings-validation', className: 'form-section' }); validation.append(element('legend', {}, t('Validation')));
    const cli = element('fieldset', { id: 'settings-cli', className: 'form-section' }); cli.append(element('legend', {}, t('CLI integration')));
    const workspace = select('settings-workspace', workspaces.map((item) => ({ value: item.id, label: item.name }))); workspace.value = settingsDraft.workspace;
    const uiLanguage = select('ui-language', [
      { value: 'auto', label: t('Automatic (VS Code)') }, { value: 'pt-BR', label: 'Português (Brasil)' }, { value: 'en-US', label: 'English' },
    ]); uiLanguage.value = settingsDraft.uiLanguage;
    const documentLanguage = select('document-language', [
      { value: 'pt-BR', label: 'Português (Brasil)' }, { value: 'en-US', label: 'English' },
    ]); documentLanguage.value = settingsDraft.documentLanguage;
    const directoryInput = element('input', { id: 'settings-directory', type: 'text', required: '', maxlength: '4096', autocomplete: 'off' }); directoryInput.value = settingsDraft.directory;
    const adrFormat = select('adr-format', [{ value: 'canonical', label: t('Canonical') }, { value: 'madr-4', label: t('MADR 4.0') }]); adrFormat.value = settingsDraft.adrFormat;
    const defaultTemplate = select('default-template', [
      { value: 'configured', label: t('Configured in the CLI') }, { value: 'minimal', label: t('Minimal') }, { value: 'extended', label: t('Extended') },
    ]); defaultTemplate.value = settingsDraft.defaultTemplate;
    const onSave = element('input', { id: 'validation-on-save', type: 'checkbox' }); onSave.checked = settingsDraft.onSave;
    const debounce = element('input', { id: 'validation-debounce', type: 'number', min: '200', max: '5000', step: '1', required: '' }); debounce.value = String(settingsDraft.debounceMilliseconds);
    debounce.disabled = !settingsDraft.onSave;
    const baseline = element('input', { id: 'validation-baseline', type: 'text', required: '', maxlength: '4096', autocomplete: 'off' }); baseline.value = settingsDraft.baseline;
    const baseReference = element('input', { id: 'validation-base-reference', type: 'text', required: '', maxlength: '256', autocomplete: 'off' }); baseReference.value = settingsDraft.baseReference;
    const cliPath = element('input', { id: 'cli-path', type: 'text', maxlength: '4096', autocomplete: 'off' }); cliPath.value = settingsDraft.cliPath;
    const timeout = element('input', { id: 'cli-timeout', type: 'number', min: '1000', max: '120000', step: '1', required: '' }); timeout.value = String(settingsDraft.timeoutMilliseconds);
    const outputLimit = element('input', { id: 'cli-output-limit', type: 'number', min: '1024', max: '1048576', step: '1', required: '' }); outputLimit.value = String(settingsDraft.maxOutputBytes);
    appendField(languages, 'ui-language', t('Interface language'), uiLanguage, t('Native VS Code commands and controls continue to use the editor language.'));
    appendField(languages, 'document-language', t('Default language for new documents'), documentLanguage);
    appendField(repository, 'settings-workspace', t('Selected workspace'), workspace);
    appendField(repository, 'settings-directory', t('ADR directory'), directoryInput, t('Use a relative path inside the selected workspace.'), true);
    appendField(repository, 'adr-format', t('ADR format'), adrFormat, t('The validation format is independent from the creation template.'));
    appendField(creation, 'default-template', t('Default creation template'), defaultTemplate, t('You can still choose another template for an individual document.'));
    appendField(validation, 'validation-on-save', t('Automatic validation on save'), onSave, t('Validation runs only after an applicable ADR file is saved.'));
    appendField(validation, 'validation-debounce', t('Validation interval (milliseconds)'), debounce, t('Accepted range: 200 to 5000 milliseconds.'));
    appendField(validation, 'validation-baseline', t('Baseline file'), baseline, t('ADR Guard only reads this existing JSON file during explicit baseline validation.'));
    appendField(validation, 'validation-base-reference', t('Default Git reference'), baseReference, t('Incremental validation lets you edit this value and never fetches automatically.'));
    appendField(cli, 'cli-path', t('ADR Guard executable'), cliPath, t('Leave empty to discover ADR Guard on the extension host PATH.'));
    const checkInstallation = button(t('Check installation'), () => {
      if (installationCheckBusy || settingsBusy) return;
      installationCheckResult = undefined;
      setInstallationCheckBusy(true);
      vscode.postMessage(listMessage('checkSettingsInstallation'));
    }, true);
    checkInstallation.id = 'check-settings-installation';
    const installationStatus = element('p', { id: 'installation-status', className: 'help', role: 'status', 'aria-live': 'polite' }, installationCheckResult || t('The check uses the saved CLI settings.'));
    const checkActions = element('div', { className: 'actions compact' }); checkActions.append(checkInstallation);
    cli.append(checkActions, installationStatus);
    const advanced = element('details', { className: 'advanced-settings' });
    advanced.append(element('summary', {}, t('Advanced settings')));
    const advancedFields = element('div', { className: 'advanced-fields' });
    appendField(advancedFields, 'cli-timeout', t('CLI timeout (milliseconds)'), timeout, t('Accepted range: 1000 to 120000 milliseconds.'));
    appendField(advancedFields, 'cli-output-limit', t('CLI output limit (bytes)'), outputLimit, t('Accepted range: 1024 to 1048576 bytes.'));
    advanced.append(advancedFields); cli.append(advanced);
    const actions = element('div', { className: 'actions form-actions' });
    const cancel = button(t('Cancel'), leaveSettings, true); cancel.id = 'settings-cancel';
    actions.append(element('button', { id: 'save-settings', type: 'submit' }, t('Save settings')), cancel);
    form.append(languages, repository, creation, validation, cli, actions); app.append(header, navigation, status, error, form);
    workspace.addEventListener('change', () => {
      const targetWorkspace = workspace.value;
      if (settingsDirty && !confirm(t('Discard the unsaved settings and switch workspace?'))) {
        workspace.value = settingsDraft.workspace;
        return;
      }
      interfacePreference = settingsBaseline.uiLanguage;
      interfaceLanguage = effectiveInterfaceLanguage(interfacePreference);
      settingsDirty = false;
      selectedWorkspace = targetWorkspace;
      persistState();
      vscode.postMessage(listMessage('openSettings'));
    });
    uiLanguage.addEventListener('change', () => {
      updateSettingsDraft();
      interfacePreference = settingsDraft.uiLanguage;
      interfaceLanguage = effectiveInterfaceLanguage(interfacePreference);
      renderSettings();
    });
    onSave.addEventListener('change', () => { debounce.disabled = !onSave.checked; updateSettingsDraft(); });
    [documentLanguage, directoryInput, adrFormat, defaultTemplate, debounce, baseline, baseReference, cliPath, timeout, outputLimit].forEach((control) => {
      control.addEventListener('input', updateSettingsDraft);
      control.addEventListener('change', updateSettingsDraft);
    });
    form.addEventListener('submit', (event) => {
      event.preventDefault();
      if (settingsBusy || installationCheckBusy || !form.reportValidity()) return;
      updateSettingsDraft();
      if (!validSettingsDirectory(settingsDraft.directory)) {
        error.textContent = t('Enter a safe relative directory inside the selected workspace.');
        error.classList.remove('hidden');
        directoryInput.focus();
        return;
      }
      if (!validBaseline(settingsDraft.baseline) || !validGitReference(settingsDraft.baseReference)
        || !validCliPath(settingsDraft.cliPath)
        || !validInteger(settingsDraft.debounceMilliseconds, 200, 5000)
        || !validInteger(settingsDraft.timeoutMilliseconds, 1000, 120000)
        || !validInteger(settingsDraft.maxOutputBytes, 1024, 1048576)) {
        error.textContent = t('Review the validation and CLI settings and enter values within the accepted limits.');
        error.classList.remove('hidden');
        return;
      }
      error.classList.add('hidden');
      setSettingsBusy(true);
      vscode.postMessage({ type: 'saveSettings', settings: settingsDraft, state: listState });
    });
    setSettingsBusy(settingsBusy);
    setInstallationCheckBusy(installationCheckBusy);
  }
  function compareEntries(left, right) {
    const fallback = left.relativePath.localeCompare(right.relativePath, 'en', { sensitivity: 'case' });
    if (listState.sort === 'title-asc') return optionalCompare(left.title, right.title, false) || fallback;
    if (listState.sort === 'date-desc') return optionalCompare(left.date, right.date, true) || fallback;
    const number = numberCompare(left.id, right.id);
    return (listState.sort === 'number-desc' ? -number : number) || fallback;
  }
  function optionalCompare(left, right, descending) { if (left === undefined) return right === undefined ? 0 : 1; if (right === undefined) return -1; const result = left.localeCompare(right, undefined, { sensitivity: 'base' }); return descending ? -result : result; }
  function numberCompare(left, right) { if (left === undefined) return right === undefined ? 0 : 1; if (right === undefined) return -1; return BigInt(left) < BigInt(right) ? -1 : BigInt(left) > BigInt(right) ? 1 : 0; }
  function renderRow(entry) {
    const row = element('article', { className: `adr-row${entry.relativePath === createdPath ? ' created' : ''}`, role: 'listitem', ...(entry.relativePath === createdPath ? { tabindex: '-1' } : {}) });
    const identity = element('span', { className: 'identifier' }, entry.id || t('No identifier'));
    const summary = element('div', { className: 'adr-summary' });
    summary.append(element('h2', {}, entry.title || t('Unknown title')), element('span', { className: 'path' }, entry.relativePath));
    const metadata = element('div', { className: 'adr-metadata' });
    metadata.append(element('span', { className: `status${entry.status ? '' : ' unknown'}` }, entry.status || t('Unknown status')));
    if (entry.date) metadata.append(element('time', { datetime: entry.date }, entry.date));
    const rowActions = element('div', { className: 'row-actions' });
    const edit = button(t('Edit'), () => {
      persistListState();
      vscode.postMessage({ type: 'editAdr', workspace: selectedWorkspace, relativePath: entry.relativePath, state: listState });
    });
    const open = button(t('Open Markdown'), () => vscode.postMessage({ type: 'openAdr', workspace: selectedWorkspace, relativePath: entry.relativePath }), true);
    rowActions.append(edit, open);
    row.append(identity, summary, metadata, rowActions);
    if (entry.warning || !entry.title) summary.append(element('span', { className: 'warning' }, t('This ADR has incomplete metadata.')));
    return row;
  }

  function sectionNavigation(items) {
    const navigation = element('nav', { className: 'form-section-nav', 'aria-label': t('Form sections') });
    items.forEach((item) => navigation.append(element('a', { href: `#${item.id}` }, t(item.label))));
    return navigation;
  }
  function appendField(container, id, label, control, help, required) {
    const group = element('div', { className: 'field' });
    const fieldLabelNode = element('label', { for: id }, label);
    if (required !== undefined) fieldLabelNode.append(element('span', { className: `requirement ${required ? 'required' : 'optional'}` }, t(required ? 'Required' : 'Optional')));
    group.append(fieldLabelNode, control);
    if (help) { const helpId = `${id}-help`; control.setAttribute('aria-describedby', helpId); group.append(element('p', { id: helpId, className: 'help' }, help)); }
    container.append(group);
  }
  function renderForm() {
    if (!trusted) return renderMessage(t('ADR Guard'), t('Trust this workspace to browse ADRs.'));
    if (!workspaces.length) return renderMessage(t('ADR Guard'), t('Open a local workspace folder to browse ADRs.'));
    clearCreationValues();
    app.replaceChildren();
    const back = button(`← ${t('Back to ADR list')}`, backToList, true);
    back.id = 'create-back'; back.classList.add('form-back');
    const heading = element('h1', {}, t('Create ADR with Form'));
    const note = element('p', { className: 'intro' }, t('No file is written until you choose Create ADR.'));
    const identity = element('p', { id: 'form-document-identity', className: 'form-document-identity' }, t('Decision title'));
    const headingGroup = element('div', { className: 'form-heading' }); headingGroup.append(heading, identity, note);
    const header = element('header', { className: 'form-header' }); header.append(back, headingGroup);
    const navigation = sectionNavigation([
      { id: 'creation-setup', label: 'Document setup' }, { id: 'creation-content', label: 'Decision content' },
      { id: 'creation-preview', label: 'Markdown preview' },
    ]);
    const statusNode = element('div', { id: 'status', role: 'status', 'aria-live': 'polite' });
    const error = element('div', { id: 'error', role: 'alert', className: 'error hidden' });
    const form = element('form', { id: 'adr-form', novalidate: '' });
    const setup = element('fieldset', { id: 'creation-setup', className: 'form-section' }); setup.append(element('legend', {}, t('Document setup')));
    const content = element('fieldset', { id: 'creation-content', className: 'form-section' }); content.append(element('legend', {}, t('Decision content')));
    const previewSection = element('section', { id: 'creation-preview', className: 'form-section preview-section', 'aria-labelledby': 'preview-heading' });
    const workspace = select('workspace', workspaces.map((item) => ({ value: item.id, label: item.name })));
    workspace.value = selectedWorkspace || workspaces[0].id;
    selectedWorkspace = workspace.value;
    const defaults = selectedWorkspaceItem();
    const directoryInput = element('input', { id: 'directory', type: 'text', required: '', autocomplete: 'off' });
    directoryInput.value = defaults.directory;
    const template = select('template', [{ value: 'configured', label: t('Configured template') }, { value: 'minimal', label: t('Minimal') }, { value: 'extended', label: t('Extended') }]);
    template.value = defaults.defaultTemplate || 'configured';
    const templateHelp = element('p', { id: 'template-help', className: 'help' }); template.setAttribute('aria-describedby', 'template-help');
    const culture = select('culture', [{ value: 'en-US', label: 'English (United States)' }, { value: 'pt-BR', label: 'Português (Brasil)' }]); culture.value = defaults.documentLanguage || 'en-US';
    const title = element('input', { id: 'title', type: 'text', required: '', maxlength: '512', autocomplete: 'off' });
    const fields = element('section', { id: 'content-fields', 'aria-label': t('Creation template') });
    const preview = element('pre', { id: 'preview', tabindex: '0' }); preview.append(element('code', {}, t('Preview unavailable until required fields are complete.')));
    const actions = element('div', { className: 'actions form-actions' });
    const submit = element('button', { id: 'create', type: 'submit' }, t('Create ADR'));
    const cancel = button(t('Cancel'), backToList, true);
    cancel.id = 'create-cancel';
    const traditional = button(t('Use traditional creation'), () => vscode.postMessage({ type: 'traditional' }), true); traditional.id = 'traditional'; traditional.classList.add('hidden');
    actions.append(submit, cancel, traditional);
    appendField(setup, 'workspace', t('Workspace folder'), workspace); appendField(setup, 'directory', t('ADR destination directory'), directoryInput);
    const templateGroup = element('div', { className: 'field' }); templateGroup.append(element('label', { for: 'template' }, t('Creation template')), template, templateHelp); setup.append(templateGroup);
    appendField(setup, 'culture', t('Document language'), culture); appendField(content, 'title', t('Decision title'), title, undefined, true);
    content.append(fields);
    previewSection.append(element('h2', { id: 'preview-heading' }, t('Markdown preview')), preview);
    form.append(setup, content, previewSection, actions); app.append(header, navigation, statusNode, error, form);
    workspace.addEventListener('change', () => {
      selectedWorkspace = workspace.value;
      const workspaceDefaults = selectedWorkspaceItem();
      directoryInput.value = workspaceDefaults.directory;
      template.value = workspaceDefaults.defaultTemplate || 'configured';
      culture.value = workspaceDefaults.documentLanguage || 'en-US';
      markDirty();
      describe();
    });
    [directoryInput, template, culture].forEach((control) => control.addEventListener('change', () => { markDirty(); describe(); }));
    title.addEventListener('input', () => { identity.textContent = title.value.trim() || t('Decision title'); markDirty(); schedulePreview(); });
    form.addEventListener('submit', (event) => { event.preventDefault(); if (!busyCreate && form.reportValidity()) { setCreateBusy(true); postForm('create'); } });
    describe();
  }

  function renderEditForm() {
    if (!editDocument) return;
    formDirty = false;
    app.replaceChildren();
    const back = button(`← ${t('Back to ADR list')}`, backToList, true);
    back.id = 'edit-back';
    back.classList.add('form-back');
    const heading = element('h1', {}, t('Edit ADR'));
    const identity = element('p', { className: 'intro path' }, editDocument.relativePath);
    const headingGroup = element('div', { className: 'form-heading' }); headingGroup.append(heading, identity);
    const header = element('header', { className: 'form-header' }); header.append(back, headingGroup);
    const navigation = sectionNavigation([
      { id: 'edit-metadata', label: 'Document metadata' }, { id: 'edit-content', label: 'Document sections' },
    ]);
    const statusNode = element('div', { id: 'status', role: 'status', 'aria-live': 'polite' });
    const error = element('div', { id: 'error', role: 'alert', className: 'error hidden' });
    const form = element('form', { id: 'adr-edit-form', novalidate: '' });
    const metadataFields = element('fieldset', { id: 'edit-metadata', className: 'form-section' }); metadataFields.append(element('legend', {}, t('Document metadata')));
    const sectionFields = element('fieldset', { id: 'edit-content', className: 'form-section' }); sectionFields.append(element('legend', {}, t('Document sections')));
    const title = element('input', { id: 'edit-title', type: 'text', required: '', maxlength: '512', autocomplete: 'off' });
    title.value = editDocument.response.title;
    appendField(metadataFields, 'edit-title', t('Decision title'), title, undefined, true);
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
      appendField(field.kind === 'section' ? sectionFields : metadataFields, id, field.label, control, undefined, field.required);
    });
    title.addEventListener('input', markDirty);
    const actions = element('div', { className: 'actions form-actions' });
    const save = element('button', { id: 'save-edit', type: 'submit' }, t('Save changes'));
    const cancel = button(t('Cancel'), backToList, true);
    cancel.id = 'edit-cancel';
    actions.append(save, cancel);
    form.append(metadataFields, sectionFields, actions);
    form.addEventListener('submit', (event) => {
      event.preventDefault();
      if (busyCreate || !form.reportValidity()) return;
      setEditBusy(true);
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
    app.append(header, navigation, statusNode, error, form);
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
  function renderSchema(response) { clearTimeout(templateTimer); schema = response.template; const fields = document.getElementById('content-fields'); const traditional = document.getElementById('traditional'); const create = document.getElementById('create'); const help = document.getElementById('template-help'); const status = document.getElementById('status'); if (!fields || !traditional || !create || !help || !status) { schema = undefined; return; } fields.replaceChildren(); if (!schema.compatible) { help.textContent = t('This custom template cannot be mapped safely to form fields. Use the traditional creation flow to preserve it.'); traditional.classList.remove('hidden'); create.disabled = true; status.textContent = ''; return; } traditional.classList.add('hidden'); create.disabled = false; help.textContent = schema.name === 'extended' ? t('Extended adds optional drivers, alternatives, rationale, risks, and references.') : t('Minimal captures the context, decision, and consequences.'); schema.fields.forEach((field) => { const id = `content-${field.id}`; const control = element('textarea', { id, rows: field.id === 'context' || field.id === 'decision' ? '7' : '5', 'data-field-id': field.id, maxlength: '32768', ...(field.required ? { required: '' } : {}) }); control.value = valuesByField[field.id] || ''; control.addEventListener('input', () => { valuesByField[field.id] = control.value; markDirty(); schedulePreview(); }); appendField(fields, id, fieldLabel(field.id), control, fieldHelp(field.id), field.required); }); status.textContent = t('Preview updates after you edit the form.'); schedulePreview(); }
  function displayTemplate(response) { try { if (!document.getElementById('adr-form')) return; hideError(); renderSchema(response); } catch { clearTimeout(templateTimer); schema = undefined; const error = document.getElementById('error'); if (error) { error.textContent = t('The template could not be displayed. Try again.'); error.classList.remove('hidden'); } const status = document.getElementById('status'); if (status) status.textContent = ''; } }
  function completeForPreview() { return schema?.compatible && document.getElementById('title').value.trim() && schema.fields.every((field) => !field.required || (valuesByField[field.id] || '').trim()); }
  function schedulePreview() { clearTimeout(previewTimer); if (!completeForPreview()) { document.querySelector('#preview code').textContent = t('Preview unavailable until required fields are complete.'); return; } const sequence = ++previewSequence; previewTimer = setTimeout(() => { if (sequence === previewSequence) postForm('preview'); }, 450); }
  function setCreateBusy(active) { busyCreate = active; document.querySelectorAll('#adr-form input, #adr-form select, #adr-form textarea, #adr-form button, .form-back').forEach((control) => { control.disabled = active; }); if (!active) document.getElementById('create').disabled = !schema?.compatible; }
  function setEditBusy(active) { busyCreate = active; document.querySelectorAll('#adr-edit-form input, #adr-edit-form select, #adr-edit-form textarea, #adr-edit-form button, .form-back').forEach((control) => { control.disabled = active; }); }
  function hideError() { const error = document.getElementById('error'); if (error) { error.textContent = ''; error.classList.add('hidden'); } }

  window.addEventListener('message', (event) => {
    const message = event.data; if (!message || typeof message.type !== 'string') return;
    if (message.type === 'initialize') { strings = message.strings || strings; editorInterfaceLanguage = message.editorInterfaceLanguage === 'pt-BR' ? 'pt-BR' : 'en-US'; hostPlatform = message.hostPlatform === 'win32' ? 'win32' : 'posix'; interfacePreference = ['auto', 'en-US', 'pt-BR'].includes(message.initialInterfacePreference) ? message.initialInterfacePreference : 'auto'; interfaceLanguage = effectiveInterfaceLanguage(interfacePreference); workspaces = Array.isArray(message.workspaces) ? message.workspaces : []; trusted = message.trusted !== false; selectedWorkspace = restored.selectedWorkspace && workspaces.some((item) => item.id === restored.selectedWorkspace) ? restored.selectedWorkspace : message.initialWorkspace; if (!trusted) renderMessage(t('ADR Guard'), t('Trust this workspace to browse ADRs.')); else if (!workspaces.length) renderMessage(t('ADR Guard'), t('Open a local workspace folder to browse ADRs.')); else if (message.initialScreen === 'form') renderForm(); else renderMessage(t('Architecture Decision Records'), t('Loading ADRs…')); return; }
    if (message.type === 'showForm') { if (message.workspace) selectedWorkspace = message.workspace; renderForm(); return; }
    if (message.type === 'showSettings') { renderSettings(message.settings); return; }
    if (message.type === 'showList') { if (message.state) listState = message.state; clearCreationValues(); formDirty = false; editDocument = undefined; settingsBaseline = undefined; settingsDraft = undefined; settingsDirty = false; settingsBusy = false; installationCheckBusy = false; installationCheckResult = undefined; renderMessage(t('Architecture Decision Records'), t('Loading ADRs…')); return; }
    if (message.type === 'requestShowList') { if (document.getElementById('settings-form')) leaveSettings(); else backToList(); return; }
    if (message.type === 'requestShowForm') {
      if (document.getElementById('settings-form')) {
        if (settingsBusy || installationCheckBusy || (settingsDirty && !confirm(t('Discard the unsaved settings and start a new ADR?')))) return;
        if (settingsBaseline) { interfacePreference = settingsBaseline.uiLanguage; interfaceLanguage = effectiveInterfaceLanguage(interfacePreference); }
        settingsDirty = false;
        vscode.postMessage(listMessage('newAdr'));
        return;
      }
      if (formDirty && !confirm(t('Discard the unsaved form data and start a new ADR?'))) return; formDirty = false; vscode.postMessage(listMessage('newAdr')); return;
    }
    if (message.type === 'editLoading') { renderMessage(t('Edit ADR'), t('Loading ADR…')); return; }
    if (message.type === 'editLoaded') { selectedWorkspace = message.workspace; editDocument = message; renderEditForm(); return; }
    if (message.type === 'listLoading') { const list = document.getElementById('adr-list'); if (list) list.setAttribute('aria-busy', 'true'); else renderMessage(t('Architecture Decision Records'), t('Loading ADRs…')); return; }
    if (message.type === 'listResult') { if (message.workspace !== selectedWorkspace) return; directory = `${workspaces.find((item) => item.id === selectedWorkspace)?.name || ''} · ${message.directory}`; entries = Array.isArray(message.entries) ? message.entries : []; if (message.state) listState = message.state; createdPath = message.createdPath; highlightKind = message.highlightKind || 'created'; renderList(); return; }
    if (message.type === 'listError') { if (message.workspace !== selectedWorkspace) return; const item = workspaces.find((candidate) => candidate.id === selectedWorkspace); directory = `${item?.name || ''} · ${message.directory || item?.directory || ''}`; renderListError(message.kind); return; }
    if (message.type === 'clearListFilters') { listState.search = ''; listState.status = ''; return; }
    if (message.type === 'created') { clearCreationValues(); formDirty = false; createdPath = message.relativePath; renderMessage(t('Architecture Decision Records'), t('Loading ADRs…')); return; }
    if (message.type === 'edited') { formDirty = false; createdPath = message.relativePath; highlightKind = 'updated'; editDocument = undefined; renderMessage(t('Architecture Decision Records'), t('Loading ADRs…')); return; }
    if (message.type === 'settingsSaved') {
      settingsBaseline = { ...message.settings };
      settingsDraft = { ...message.settings };
      settingsDirty = false;
      interfacePreference = message.settings.uiLanguage;
      interfaceLanguage = effectiveInterfaceLanguage(interfacePreference);
      const workspace = workspaces.find((item) => item.id === message.settings.workspace);
      if (workspace) { workspace.directory = message.settings.directory; workspace.documentLanguage = message.settings.documentLanguage; workspace.defaultTemplate = message.settings.defaultTemplate; }
      return;
    }
    if (message.type === 'templateLoaded') { displayTemplate(message.response); return; }
    if (message.type === 'result') { if (!document.getElementById('adr-form')) return; hideError(); if (message.response.operation === 'preview') document.querySelector('#preview code').textContent = message.response.markdown || ''; return; }
    if (message.type === 'error' || message.type === 'listNavigationError' || message.type === 'editError') { const error = document.getElementById('error'); if (error) { error.textContent = message.message || 'Error'; error.classList.remove('hidden'); if (message.traditionalAvailable) document.getElementById('traditional')?.classList.remove('hidden'); } else if (message.type === 'editError') { renderMessage(t('Edit ADR'), message.message || 'Error', button(t('Back to ADR list'), backToList, true)); } else window.alert(message.message || 'Error'); return; }
    if (message.type === 'busy' && message.operation === 'create') { setCreateBusy(true); document.getElementById('status').textContent = t('Creating ADR…'); }
    if (message.type === 'idle' && message.operation === 'create') setCreateBusy(false);
    if (message.type === 'editBusy') { setEditBusy(true); document.getElementById('status').textContent = t('Saving changes…'); }
    if (message.type === 'editIdle') setEditBusy(false);
    if (message.type === 'settingsBusy') setSettingsBusy(true);
    if (message.type === 'settingsIdle') setSettingsBusy(false);
    if (message.type === 'settingsError') { const error = document.getElementById('error'); if (error) { error.textContent = message.message || 'Error'; error.classList.remove('hidden'); } }
    if (message.type === 'installationChecking') setInstallationCheckBusy(true);
    if (message.type === 'installationResult') { installationCheckResult = message.result?.available === true && message.result.version ? t('ADR Guard {0} is available ({1}).', message.result.version, message.result.source === 'configured' ? t('configured path') : 'PATH') : message.result?.message || t('Installation check failed.'); const status = document.getElementById('installation-status'); if (status) { status.textContent = installationCheckResult; status.classList.toggle('error-text', message.result?.available !== true); } }
    if (message.type === 'installationCheckIdle') setInstallationCheckBusy(false);
  });
  renderMessage('', '');
  vscode.postMessage({ type: 'ready' });
}());
