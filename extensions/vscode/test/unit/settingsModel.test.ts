import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  applyConfigurationWrites,
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
    onSave: true,
    debounceMilliseconds: 900,
    baseline: '.adrguard-baseline.json',
    baseReference: 'origin/main',
    cliPath: process.platform === 'win32' ? 'C:\\tools\\adr-guard.exe' : '/opt/adr-guard',
    timeoutMilliseconds: 30000,
    maxOutputBytes: 524288,
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
    assert.equal(parseVisualSettingsValues({ ...values, debounceMilliseconds: 199 }), undefined);
    assert.equal(parseVisualSettingsValues({ ...values, debounceMilliseconds: 5001 }), undefined);
    assert.equal(parseVisualSettingsValues({ ...values, baseline: '../baseline.json' }), undefined);
    assert.equal(parseVisualSettingsValues({ ...values, baseline: 'baseline.txt' }), undefined);
    assert.equal(parseVisualSettingsValues({ ...values, baseReference: '--all' }), undefined);
    assert.equal(parseVisualSettingsValues({ ...values, cliPath: 'workspace/tool' }), undefined);
    assert.equal(parseVisualSettingsValues({ ...values, timeoutMilliseconds: 999 }), undefined);
    assert.equal(parseVisualSettingsValues({ ...values, maxOutputBytes: 1048577 }), undefined);
    assert.equal(parseVisualSettingsValues({ ...values, extra: true }), undefined);
  });

  it('maps each preference to its required VS Code configuration scope', () => {
    assert.deepEqual(configurationWrites(values), [
      { section: 'adrGuard.ui', key: 'language', value: 'auto', target: 'global' },
      { section: 'adrGuard.creation', key: 'documentLanguage', value: 'pt-BR', target: 'workspaceFolder' },
      { section: 'adrGuard.creation', key: 'defaultTemplate', value: 'extended', target: 'workspaceFolder' },
      { section: 'adrGuard.validation', key: 'directory', value: 'docs/decisions', target: 'workspaceFolder' },
      { section: 'adrGuard.validation', key: 'adrFormat', value: 'madr-4', target: 'workspaceFolder' },
      { section: 'adrGuard.validation', key: 'onSave', value: true, target: 'workspaceFolder' },
      { section: 'adrGuard.validation', key: 'debounceMilliseconds', value: 900, target: 'workspaceFolder' },
      { section: 'adrGuard.validation', key: 'baseline', value: '.adrguard-baseline.json', target: 'workspaceFolder' },
      { section: 'adrGuard.validation', key: 'baseReference', value: 'origin/main', target: 'workspaceFolder' },
      { section: 'adrGuard.cli', key: 'path', value: values.cliPath, target: 'global' },
      { section: 'adrGuard.cli', key: 'timeoutMilliseconds', value: 30000, target: 'global' },
      { section: 'adrGuard.cli', key: 'maxOutputBytes', value: 524288, target: 'global' },
    ]);
  });

  it('rolls back earlier settings when a later VS Code configuration write fails', async () => {
    const writes = configurationWrites(values).slice(0, 3);
    const stored = new Map(writes.map((write) => [`${write.section}.${write.key}`, `before-${write.key}`]));
    const updates: string[] = [];

    await assert.rejects(
      applyConfigurationWrites(
        writes,
        (write) => stored.get(`${write.section}.${write.key}`),
        (write, value) => {
          const key = `${write.section}.${write.key}`;
          updates.push(`${key}=${String(value)}`);
          if (write.key === 'defaultTemplate' && value === 'extended') return Promise.reject(new Error('controlled write failure'));
          stored.set(key, value as string);
          return Promise.resolve();
        },
      ),
      /controlled write failure/u,
    );

    assert.equal(stored.get('adrGuard.ui.language'), 'before-language');
    assert.equal(stored.get('adrGuard.creation.documentLanguage'), 'before-documentLanguage');
    assert.deepEqual(updates.slice(-2), [
      'adrGuard.creation.documentLanguage=before-documentLanguage',
      'adrGuard.ui.language=before-language',
    ]);
  });

  it('resolves automatic Webview language from the editor without coupling document language', () => {
    assert.equal(effectiveInterfaceLanguage('auto', 'pt-br'), 'pt-BR');
    assert.equal(effectiveInterfaceLanguage('auto', 'en'), 'en-US');
    assert.equal(effectiveInterfaceLanguage('en-US', 'pt-br'), 'en-US');
    assert.equal(effectiveInterfaceLanguage('pt-BR', 'en'), 'pt-BR');
  });
});
