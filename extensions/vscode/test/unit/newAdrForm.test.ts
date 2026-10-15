import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { describe, it } from 'node:test';
import { parseNewAdrFormMessage, visualFormInputLimit, VisualCreationSubmission } from '../../src/webview/newAdrFormMessages';

describe('visual ADR form host boundary', () => {
  const values = {
    workspace: 'file:///workspace',
    directory: 'docs/adr',
    template: 'minimal',
    culture: 'pt-BR',
    title: 'Adotar fila',
    fields: {
      context: 'Contexto com acentos',
      decision: 'Decisão',
      consequences: 'Consequências',
    },
  };
  const settings = {
    workspace: 'file:///workspace', uiLanguage: 'auto' as const, documentLanguage: 'pt-BR' as const,
    directory: 'docs/adr', adrFormat: 'canonical' as const, defaultTemplate: 'extended' as const,
    onSave: false, debounceMilliseconds: 750, baseline: '.adrguard-baseline.json', baseReference: 'main',
    cliPath: '', timeoutMilliseconds: 15000, maxOutputBytes: 262144,
  };

  it('accepts only bounded known message shapes', () => {
    assert.deepEqual(parseNewAdrFormMessage({ type: 'preview', values }), { type: 'preview', values });
    assert.deepEqual(parseNewAdrFormMessage({ type: 'cancel' }), { type: 'cancel' });
    assert.deepEqual(parseNewAdrFormMessage({
      type: 'cancel', workspace: 'file:///workspace',
      state: { search: 'api', status: 'Accepted', sort: 'number-desc', scrollTop: 42 },
    }), {
      type: 'cancel', workspace: 'file:///workspace',
      state: { search: 'api', status: 'Accepted', sort: 'number-desc', scrollTop: 42 },
    });
    assert.deepEqual(parseNewAdrFormMessage({ type: 'openAdr', workspace: 'file:///workspace', relativePath: 'nested/0001-safe.md' }), {
      type: 'openAdr', workspace: 'file:///workspace', relativePath: 'nested/0001-safe.md',
    });
    assert.deepEqual(parseNewAdrFormMessage({
      type: 'newAdr', workspace: 'file:///workspace',
      state: { search: 'api', status: 'Accepted', sort: 'number-desc', scrollTop: 42 },
    }), {
      type: 'newAdr', workspace: 'file:///workspace',
      state: { search: 'api', status: 'Accepted', sort: 'number-desc', scrollTop: 42 },
    });
    assert.deepEqual(parseNewAdrFormMessage({
      type: 'editAdr', workspace: 'file:///workspace', relativePath: '0001-safe.md',
      state: { search: '', status: '', sort: 'number-desc', scrollTop: 0 },
    }), {
      type: 'editAdr', workspace: 'file:///workspace', relativePath: '0001-safe.md',
      state: { search: '', status: '', sort: 'number-desc', scrollTop: 0 },
    });
    const editValues = {
      workspace: 'file:///workspace', relativePath: '0001-safe.md', fingerprint: 'A'.repeat(64),
      title: 'Safe', fields: { 'section-0': 'Accepted' },
    };
    assert.deepEqual(parseNewAdrFormMessage({ type: 'saveEdit', editValues }), { type: 'saveEdit', editValues });
    assert.deepEqual(parseNewAdrFormMessage({ type: 'saveSettings', settings, state: { search: '', status: '', sort: 'number-desc', scrollTop: 0 } }), {
      type: 'saveSettings', settings, state: { search: '', status: '', sort: 'number-desc', scrollTop: 0 },
    });
    for (const type of ['showList', 'selectWorkspace', 'refreshList', 'initializeRepository', 'checkInstallation', 'openSettings', 'cancelSettings', 'checkSettingsInstallation'] as const) {
      assert.deepEqual(parseNewAdrFormMessage({
        type, workspace: 'file:///workspace',
        state: { search: 'api', status: 'Accepted', sort: 'number-desc', scrollTop: 42 },
      }), {
        type, workspace: 'file:///workspace',
        state: { search: 'api', status: 'Accepted', sort: 'number-desc', scrollTop: 42 },
      });
    }
    for (const invalid of [
      null,
      { type: 'unknown' },
      { type: 'cancel', values },
      { type: 'cancel', workspace: 'file:///workspace', state: { search: '', status: '', sort: 'invalid', scrollTop: 0 } },
      { type: 'create', values: { ...values, directory: '../outside' }, extra: true },
      { type: 'preview', values: { ...values, culture: 'fr-FR' } },
      { type: 'preview', values: { ...values, fields: { '<script>': 'alert(1)' } } },
      { type: 'preview', values: { ...values, title: 'x'.repeat(513) } },
      { type: 'openAdr', workspace: 'file:///workspace', relativePath: '../outside.md' },
      { type: 'openAdr', workspace: 'file:///workspace', relativePath: 'safe.txt' },
      { type: 'editAdr', workspace: 'file:///workspace', relativePath: '../outside.md' },
      { type: 'saveEdit', editValues: { workspace: 'file:///workspace', relativePath: '0001-safe.md', fingerprint: 'bad', title: 'Safe', fields: {} } },
      { type: 'saveEdit', editValues: { workspace: 'file:///workspace', relativePath: '0001-safe.md', fingerprint: 'A'.repeat(64), title: 'Safe', fields: { '../bad': 'x' } } },
      { type: 'saveSettings', settings: { ...settings, directory: '../outside' } },
      { type: 'saveSettings', settings: { ...settings, uiLanguage: 'fr-FR' } },
    ]) assert.equal(parseNewAdrFormMessage(invalid), undefined);
  });

  it('blocks duplicate creation until the active submission settles', () => {
    const submission = new VisualCreationSubmission();
    assert.equal(submission.accepts(), true);
    assert.equal(submission.begin(), true);
    assert.equal(submission.begin(), false);
    assert.equal(submission.accepts(), false);
    assert.equal(submission.isActive, true);
    submission.end();
    assert.equal(submission.isActive, false);
    assert.equal(submission.accepts(), true);
    assert.equal(submission.begin(), true);
  });

  it('rejects aggregate form payloads that exceed their CLI contract', () => {
    const largeValue = 'x'.repeat(32 * 1024);
    const creation = {
      type: 'preview',
      values: {
        ...values,
        fields: Object.fromEntries(Array.from({ length: 5 }, (_, index) => [`field${index}`, largeValue])),
      },
    };
    const edit = {
      type: 'saveEdit',
      editValues: {
        workspace: 'file:///workspace', relativePath: '0001-safe.md', fingerprint: 'A'.repeat(64), title: 'Safe',
        fields: Object.fromEntries(Array.from({ length: 13 }, (_, index) => [`section-${index}`, largeValue])),
      },
    };

    assert.equal(visualFormInputLimit(creation), 'creation');
    assert.equal(parseNewAdrFormMessage(creation), undefined);
    assert.equal(visualFormInputLimit(edit), 'edit');
    assert.equal(parseNewAdrFormMessage(edit), undefined);
  });

  it('renders untrusted Markdown only through textContent', async () => {
    const source = await readFile(path.resolve(__dirname, '../../../media/newAdrForm.js'), 'utf8');
    assert.match(source, /textContent = message\.response\.markdown/u);
    assert.doesNotMatch(source, /innerHTML/u);
    assert.match(source, /function clearCreationValues\(\)/u);
    assert.match(source, /backToList\(\).*type: 'cancel'/u);
    assert.doesNotMatch(source, /backToList\(\).*workspace: selectedWorkspace/u);
    assert.doesNotMatch(source, /backToList\(\).*state: listState/u);
    assert.doesNotMatch(source, /backToList\(\).*clearCreationValues\(\)/u);
    assert.match(source, /message\.type === 'showList'.*clearCreationValues\(\)/u);
    assert.match(source, /message\.type === 'templateLoaded'.*displayTemplate\(message\.response\)/u);
    assert.match(source, /function displayTemplate\(response\).*renderSchema\(response\)/u);
    assert.match(source, /templateTimer = setTimeout/u);
    assert.match(source, /select\('ui-language'/u);
    assert.match(source, /vscode\.postMessage\(listMessage\('openSettings'\)\)/u);
    assert.doesNotMatch(source, /aria-pressed/u);
    assert.match(source, /listMessage\('initializeRepository'\)/u);
    assert.match(source, /listMessage\('checkInstallation'\)/u);
    assert.match(source, /entries\.length > 0 \? 'initialized' : 'available'/u);
    assert.match(source, /className: 'disabled-button-wrapper', title: hint/u);
    assert.doesNotMatch(source, /restored\.interfaceLanguage/u);
    assert.match(source, /let listState = normalizeListState\(restored\.listState\)/u);
    assert.match(source, /const targetScroll = listState\.scrollTop \|\| 0/u);
    assert.match(source, /window\.scrollTo\(0, targetScroll\)/u);
  });

  it('resolves the selected workspace before the form control is mounted', async () => {
    const source = await readFile(path.resolve(__dirname, '../../../media/newAdrForm.js'), 'utf8');
    assert.match(source, /workspaceControl \? workspaceControl\.value : selectedWorkspace/u);
    const selectedWorkspaceFunction = /function selectedWorkspaceItem\(\) \{(?<body>[\s\S]*?)\n[ ]{2}\}/u.exec(source)?.groups?.body;
    assert.ok(selectedWorkspaceFunction);
    assert.doesNotMatch(selectedWorkspaceFunction, /document\.getElementById\('workspace'\)\.value/u);
  });

  it('renders compact accessible rows with responsive VS Code theme styling', async () => {
    const script = await readFile(path.resolve(__dirname, '../../../media/newAdrForm.js'), 'utf8');
    const styles = await readFile(path.resolve(__dirname, '../../../media/newAdrForm.css'), 'utf8');

    assert.match(script, /className: `adr-row\$\{entry\.relativePath === createdPath \? ' created' : ''\}`/u);
    assert.match(script, /role: 'listitem'/u);
    assert.match(script, /list\.setAttribute\('role', 'list'\)/u);
    assert.match(script, /row\.append\(identity, summary, metadata, rowActions\)/u);
    assert.doesNotMatch(script, /adr-card/u);
    assert.match(styles, /\.adr-row \{[\s\S]*?grid-template-columns:/u);
    assert.match(styles, /var\(--vscode-list-hoverBackground\)/u);
    assert.match(styles, /var\(--vscode-badge-background\)/u);
    assert.match(styles, /var\(--vscode-contrastBorder, transparent\)/u);
    assert.match(styles, /@media \(max-width: 760px\)/u);
    assert.match(styles, /@media \(max-width: 480px\)[\s\S]*?\.row-actions/u);
    assert.doesNotMatch(styles, /#[0-9a-fA-F]{3,8}\b/u);
  });

  it('keeps form navigation and actions accessible, responsive, and theme-aware', async () => {
    const script = await readFile(path.resolve(__dirname, '../../../media/newAdrForm.js'), 'utf8');
    const styles = await readFile(path.resolve(__dirname, '../../../media/newAdrForm.css'), 'utf8');

    assert.match(script, /function sectionNavigation\(items\)/u);
    assert.match(script, /className: 'form-section-nav', 'aria-label': t\('Form sections'\)/u);
    assert.match(script, /className: `requirement \$\{required \? 'required' : 'optional'\}`/u);
    assert.match(styles, /@media \(min-width: 700px\) and \(min-height: 500px\)[\s\S]*?\.form-actions[\s\S]*?position: sticky/u);
    assert.match(styles, /\.form-section-nav a:focus-visible[\s\S]*?var\(--vscode-focusBorder\)/u);
    assert.match(styles, /@media \(max-width: 480px\)[\s\S]*?\.form-header[\s\S]*?display: grid/u);
  });

  it('sends isolated list actions and captures state before refresh or navigation', async () => {
    const source = await readFile(path.resolve(__dirname, '../../../media/newAdrForm.js'), 'utf8');

    assert.match(source, /const refresh = button\(t\('Refresh'\), \(\) => \{ persistListState\(\); vscode\.postMessage\(listMessage\('refreshList'\)\); \}/u);
    assert.match(source, /vscode\.postMessage\(\{ type: 'openAdr', workspace: selectedWorkspace, relativePath: entry\.relativePath \}\)/u);
    assert.match(source, /vscode\.postMessage\(\{ type: 'editAdr', workspace: selectedWorkspace, relativePath: entry\.relativePath, state: listState \}\)/u);
    assert.match(source, /vscode\.postMessage\(listMessage\('newAdr'\)\)/u);
    assert.match(source, /vscode\.setState\(\{ listState, selectedWorkspace \}\)/u);
    assert.match(source, /const targetScroll = listState\.scrollTop \|\| 0/u);
    assert.match(source, /window\.scrollTo\(0, targetScroll\)/u);
  });

  it('keeps list loading, refresh, and Markdown navigation free of CLI validation and writes', async () => {
    const host = await readFile(path.resolve(__dirname, '../../../src/commands/newAdrForm.ts'), 'utf8');
    const cancelRoute = host.slice(host.indexOf("if (message.type === 'showList'"), host.indexOf("if (message.type === 'selectWorkspace'"));
    const loadList = host.slice(host.indexOf('private async loadList('), host.indexOf('private async openAdr('));
    const openAdr = host.slice(host.indexOf('private async openAdr('), host.indexOf('private async runListAction('));
    const refreshRoute = host.slice(host.indexOf("if (message.type === 'selectWorkspace'"), host.indexOf("if (message.type === 'initializeRepository'"));

    assert.match(cancelRoute, /if \(this\.submission\.isActive\) return/u);
    assert.match(cancelRoute, /await this\.loadList\(this\.selectedWorkspace, message\.state\)/u);
    assert.doesNotMatch(cancelRoute, /executor\.execute|executeCommand|saveEdit|this\.run\(/u);
    assert.match(loadList, /explorer\.catalogFor\(folder\)/u);
    assert.match(loadList, /this\.screen !== 'list'/u);
    assert.doesNotMatch(loadList, /executor\.execute|executeCommand|\['check'|\['new'|\['edit'|\['index'/u);
    assert.match(openAdr, /showTextDocument\(vscode\.Uri\.file\(target\.filePath\)/u);
    assert.doesNotMatch(openAdr, /executor\.execute|executeCommand|writeFile|rename|unlink/u);
    assert.match(refreshRoute, /await this\.loadList\(message\.workspace, message\.state\)/u);
    assert.doesNotMatch(refreshRoute, /executor\.execute|executeCommand/u);
  });

  it('negotiates configured-root support before visual edit validation', async () => {
    const host = await readFile(path.resolve(__dirname, '../../../src/commands/newAdrForm.ts'), 'utf8');
    const loadEdit = host.slice(host.indexOf('private async loadEdit('), host.indexOf('private async saveEdit('));
    const saveEdit = host.slice(host.indexOf('private async saveEdit('), host.indexOf('private async resolveCatalogAdr('));
    const resolveTarget = host.slice(host.indexOf('private async resolveCatalogAdr('), host.indexOf('private async run('));

    assert.match(loadEdit, /this\.visualEditArguments\(target, controller\.signal\)/u);
    assert.match(saveEdit, /this\.visualEditArguments\(target, controller\.signal\)/u);
    assert.match(resolveTarget, /return \{ folder, directory, filePath: candidate/u);
    assert.match(resolveTarget, /\['edit', '--help'\]/u);
    assert.match(resolveTarget, /buildVisualEditArguments\(target\.filePath, target\.directory, target\.format, help\.result\.stdout\)/u);
  });

  it('keeps settings navigation and persistence isolated from ADR commands', async () => {
    const host = await readFile(path.resolve(__dirname, '../../../src/commands/newAdrForm.ts'), 'utf8');
    const settingsRoutes = host.slice(host.indexOf("if (message.type === 'openSettings'"), host.indexOf("if (message.type === 'newAdr'"));
    const saveSettings = host.slice(host.indexOf('private async saveSettings('), host.indexOf('private async loadList('));

    assert.match(settingsRoutes, /this\.screen !== 'list' && this\.screen !== 'settings'/u);
    assert.match(settingsRoutes, /settingsFor\(workspaceFolder\(message\.workspace\)\)/u);
    assert.match(settingsRoutes, /message\.type === 'cancelSettings'/u);
    assert.match(settingsRoutes, /message\.type === 'checkSettingsInstallation'/u);
    assert.match(settingsRoutes, /this\.checkSettingsInstallation\(message\.workspace\)/u);
    assert.match(saveSettings, /ConfigurationTarget\.Global/u);
    assert.match(saveSettings, /ConfigurationTarget\.WorkspaceFolder/u);
    assert.match(saveSettings, /getConfiguration\(write\.section, scope\)\.update/u);
    assert.doesNotMatch(`${settingsRoutes}\n${saveSettings}`, /executor\.execute|executeCommand|\['check'|\['new'|\['edit'|\['index'|writeFile|rename|unlink/u);
    const explicitInstallationCheck = saveSettings.slice(saveSettings.indexOf('private async checkSettingsInstallation('));
    assert.match(explicitInstallationCheck, /dependencies\.checkInstallation\(workspaceFolder\(workspace\), false\)/u);
  });

  it('uses the persisted Git reference only as the editable incremental-validation default', async () => {
    const source = await readFile(path.resolve(__dirname, '../../../src/commands/coreCommands.ts'), 'utf8');
    const changed = source.slice(source.indexOf('export async function validateChangedAdrs'), source.indexOf('export async function validateWithBaseline'));
    assert.match(changed, /value: configuration\.baseReference/u);
    assert.match(changed, /validateInput: \(value\) => isSafeGitBaseReference\(value\.trim\(\)\)/u);
    assert.match(changed, /baseReference: baseReference\.trim\(\)/u);
    assert.match(changed, /No fetch will be performed/u);
    assert.doesNotMatch(changed, /git\s+fetch|args:\s*\['fetch'/u);
  });

  it('keeps automatic validation gated by document saves, the toggle, and the configured debounce', async () => {
    const source = await readFile(path.resolve(__dirname, '../../../src/validation/onSave.ts'), 'utf8');
    assert.match(source, /onDidSaveTextDocument\(\(document\) => \{ void this\.handleSavedDocument\(document\); \}\)/u);
    assert.match(source, /if \(!configuration\.onSave\) return/u);
    assert.match(source, /scheduler\.schedule\(configuration\.debounceMilliseconds\)/u);
  });
});
