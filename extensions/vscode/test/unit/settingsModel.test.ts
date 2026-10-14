import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  configurationWrites,
  effectiveInterfaceLanguage,
  isSafeRelativeDirectory,
  parseVisualSettingsValues,
} from '../../src/webview/settingsModel';

describe('visual settings model', () => {
  const values = {
    workspace: 'file:///workspace',
    uiLanguage: 'auto' as const,
    documentLanguage: 'pt-BR' as const,
    directory: 'docs/decisions',
    adrFormat: 'madr-4' as const,
    defaultTemplate: 'extended' as const,
  };

  it('accepts the supported values and rejects unsafe paths or unknown options', () => {
    assert.deepEqual(parseVisualSettingsValues(values), values);
    for (const directory of ['../outside', '/absolute', 'C:\\outside', '\\\\server\\share', 'docs/../outside', '', '   ']) {
      assert.equal(isSafeRelativeDirectory(directory), false, directory);
      assert.equal(parseVisualSettingsValues({ ...values, directory }), undefined);
    }
    assert.equal(parseVisualSettingsValues({ ...values, uiLanguage: 'fr-FR' }), undefined);
    assert.equal(parseVisualSettingsValues({ ...values, documentLanguage: 'auto' }), undefined);
    assert.equal(parseVisualSettingsValues({ ...values, adrFormat: 'custom' }), undefined);
    assert.equal(parseVisualSettingsValues({ ...values, defaultTemplate: 'custom' }), undefined);
    assert.equal(parseVisualSettingsValues({ ...values, extra: true }), undefined);
  });

  it('maps each preference to its required VS Code configuration scope', () => {
    assert.deepEqual(configurationWrites(values), [
      { section: 'adrGuard.ui', key: 'language', value: 'auto', target: 'global' },
      { section: 'adrGuard.creation', key: 'documentLanguage', value: 'pt-BR', target: 'workspaceFolder' },
      { section: 'adrGuard.creation', key: 'defaultTemplate', value: 'extended', target: 'workspaceFolder' },
      { section: 'adrGuard.validation', key: 'directory', value: 'docs/decisions', target: 'workspaceFolder' },
      { section: 'adrGuard.validation', key: 'adrFormat', value: 'madr-4', target: 'workspaceFolder' },
    ]);
  });

  it('resolves automatic Webview language from the editor without coupling document language', () => {
    assert.equal(effectiveInterfaceLanguage('auto', 'pt-br'), 'pt-BR');
    assert.equal(effectiveInterfaceLanguage('auto', 'en'), 'en-US');
    assert.equal(effectiveInterfaceLanguage('en-US', 'pt-br'), 'en-US');
    assert.equal(effectiveInterfaceLanguage('pt-BR', 'en'), 'pt-BR');
  });
});
