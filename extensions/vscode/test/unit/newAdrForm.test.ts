import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { describe, it } from 'node:test';
import { parseNewAdrFormMessage, VisualCreationSubmission } from '../../src/webview/newAdrFormMessages';

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
    for (const invalid of [
      null,
      { type: 'unknown' },
      { type: 'cancel', values },
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

  it('renders untrusted Markdown only through textContent', async () => {
    const source = await readFile(path.resolve(__dirname, '../../../media/newAdrForm.js'), 'utf8');
    assert.match(source, /textContent = message\.response\.markdown/u);
    assert.doesNotMatch(source, /innerHTML/u);
    assert.match(source, /function clearCreationValues\(\)/u);
    assert.match(source, /backToList\(\).*clearCreationValues\(\)/u);
  });
});
