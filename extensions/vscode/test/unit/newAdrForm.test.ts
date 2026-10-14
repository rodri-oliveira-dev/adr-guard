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
    for (const type of ['showList', 'selectWorkspace', 'refreshList', 'initializeRepository', 'checkInstallation'] as const) {
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
    ]) assert.equal(parseNewAdrFormMessage(invalid), undefined);
  });

  it('blocks duplicate creation until the active submission settles', () => {
    const submission = new VisualCreationSubmission();
    assert.equal(submission.accepts('preview'), true);
    assert.equal(submission.begin(), true);
    assert.equal(submission.begin(), false);
    assert.equal(submission.accepts('describe'), false);
    assert.equal(submission.accepts('preview'), false);
    assert.equal(submission.accepts('create'), false);
    assert.equal(submission.accepts('traditional'), false);
    assert.equal(submission.accepts('cancel'), true);
    submission.end();
    assert.equal(submission.accepts('preview'), true);
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
    assert.match(source, /select\('interface-language'/u);
    assert.match(source, /language\.classList\.add\('language-switch'\)/u);
    assert.match(source, /language\.setAttribute\('aria-label', t\('Interface language'\)\)/u);
    assert.match(source, /language\.addEventListener\('change'/u);
    assert.match(source, /interfaceLanguage = language\.value === 'pt-BR' \? 'pt-BR' : 'en-US'/u);
    assert.doesNotMatch(source, /aria-pressed/u);
    assert.match(source, /listMessage\('initializeRepository'\)/u);
    assert.match(source, /listMessage\('checkInstallation'\)/u);
    assert.match(source, /entries\.length > 0 \? 'initialized' : 'available'/u);
    assert.match(source, /className: 'disabled-button-wrapper', title: hint/u);
    assert.match(source, /restored\.interfaceLanguage === 'pt-BR'/u);
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

  it('sends isolated list actions and captures state before refresh or navigation', async () => {
    const source = await readFile(path.resolve(__dirname, '../../../media/newAdrForm.js'), 'utf8');

    assert.match(source, /const refresh = button\(t\('Refresh'\), \(\) => \{ persistListState\(\); vscode\.postMessage\(listMessage\('refreshList'\)\); \}/u);
    assert.match(source, /vscode\.postMessage\(\{ type: 'openAdr', workspace: selectedWorkspace, relativePath: entry\.relativePath \}\)/u);
    assert.match(source, /vscode\.postMessage\(\{ type: 'editAdr', workspace: selectedWorkspace, relativePath: entry\.relativePath, state: listState \}\)/u);
    assert.match(source, /vscode\.postMessage\(listMessage\('newAdr'\)\)/u);
    assert.match(source, /vscode\.setState\(\{ listState, selectedWorkspace, interfaceLanguage \}\)/u);
    assert.match(source, /const targetScroll = listState\.scrollTop \|\| 0/u);
    assert.match(source, /window\.scrollTo\(0, targetScroll\)/u);
  });

  it('keeps list loading, refresh, and Markdown navigation free of CLI validation and writes', async () => {
    const host = await readFile(path.resolve(__dirname, '../../../src/commands/newAdrForm.ts'), 'utf8');
    const loadList = host.slice(host.indexOf('private async loadList('), host.indexOf('private async openAdr('));
    const openAdr = host.slice(host.indexOf('private async openAdr('), host.indexOf('private async runListAction('));
    const refreshRoute = host.slice(host.indexOf("if (message.type === 'selectWorkspace'"), host.indexOf("if (message.type === 'initializeRepository'"));

    assert.match(loadList, /explorer\.catalogFor\(folder\)/u);
    assert.doesNotMatch(loadList, /executor\.execute|executeCommand|\['check'|\['new'|\['edit'|\['index'/u);
    assert.match(openAdr, /showTextDocument\(vscode\.Uri\.file\(target\.filePath\)/u);
    assert.doesNotMatch(openAdr, /executor\.execute|executeCommand|writeFile|rename|unlink/u);
    assert.match(refreshRoute, /await this\.loadList\(message\.workspace, message\.state\)/u);
    assert.doesNotMatch(refreshRoute, /executor\.execute|executeCommand/u);
  });
});
