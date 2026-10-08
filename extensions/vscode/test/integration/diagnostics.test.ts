import assert from 'node:assert/strict';
import * as vscode from 'vscode';
import type { CliExecutor } from '../../src/cli/executor';
import type { CliRunResult } from '../../src/cli/runner';
import type { OperationalLog } from '../../src/logging';
import { DiagnosticManager } from '../../src/validation/diagnostics';
import { SaveValidationController } from '../../src/validation/onSave';

suite('ADR Guard Problems diagnostics', () => {
  test('publishes ADR005 at document start and clears it after correction', async () => {
    const folder = vscode.workspace.workspaceFolders?.[0];
    assert.ok(folder);
    const uri = vscode.Uri.joinPath(folder.uri, 'docs', 'adr', '0001-invalid.md');
    const fake = new FakeExecutor(folder.uri.fsPath);
    const logMessages: string[] = [];
    const manager = new DiagnosticManager(
      fake as unknown as CliExecutor,
      { info: (message: string) => logMessages.push(message) } as unknown as OperationalLog,
    );
    try {
      await manager.validate(folder, 'docs/adr');
      assert.equal(fake.lastArguments[0], 'check');
      assert.equal(fake.lastArguments[1]?.toLowerCase(), vscode.Uri.joinPath(folder.uri, 'docs', 'adr').fsPath.toLowerCase());
      assert.deepEqual(fake.lastArguments.slice(2), ['--format', 'json']);
      assert.equal(fake.lastArguments.includes('index'), false);
      const diagnostics = vscode.languages.getDiagnostics(uri);
      assert.equal(diagnostics.length, 1);
      const diagnostic = diagnostics[0];
      assert.ok(diagnostic);
      assert.equal(diagnostic.code, 'ADR005');
      assert.equal(diagnostic.message, "ADR must define a non-empty 'Decision' section.");
      assert.equal(diagnostic.source, 'ADR Guard');
      assert.equal(diagnostic.severity, vscode.DiagnosticSeverity.Error);
      assert.equal(diagnostic.range.start.line, 0);
      assert.equal(diagnostic.range.start.character, 0);
      assert.equal((await vscode.workspace.openTextDocument(uri)).uri.toString(), uri.toString());

      fake.report = validReport();
      fake.exitCode = 0;
      await manager.validate(folder, 'docs/adr');
      assert.deepEqual(vscode.languages.getDiagnostics(uri), []);
      assert.ok(logMessages.some((message) => message.includes('Published 0')));
    } finally {
      manager.dispose();
    }
  });

  test('passes MADR, incremental, base-ref and baseline as literal CLI arguments', async () => {
    const folder = vscode.workspace.workspaceFolders?.[0];
    assert.ok(folder);
    const fake = new FakeExecutor(folder.uri.fsPath);
    fake.report = baselineReport();
    fake.exitCode = 0;
    const manager = new DiagnosticManager(
      fake as unknown as CliExecutor,
      { info: () => undefined } as unknown as OperationalLog,
    );
    try {
      const result = await manager.validate(folder, 'docs/adr', undefined, {
        adrFormat: 'madr-4',
        changed: true,
        baseReference: 'origin/main',
        baselineSetting: '.adrguard-baseline.json',
      });
      assert.equal(result.report.valid, true, 'existing baseline findings do not fail validation');
      assert.deepEqual(fake.lastArguments.slice(2).map((argument) =>
        process.platform === 'win32' && pathLike(argument) ? argument.toLowerCase() : argument), [
        '--adr-format', 'madr-4', '--changed', '--base-ref', 'origin/main',
        '--baseline', vscode.Uri.joinPath(folder.uri, '.adrguard-baseline.json').fsPath.toLowerCase(),
        '--format', 'json',
      ]);
      const uri = vscode.Uri.joinPath(folder.uri, 'docs', 'adr', '0001-invalid.md');
      assert.match(vscode.languages.getDiagnostics(uri)[0]?.message ?? '', /Existing in baseline/u);
      assert.deepEqual(result.report.baseline, { new: 0, existing: 1, resolved: 2 });

      fake.report = newBaselineReport();
      fake.exitCode = 1;
      const failed = await manager.validate(folder, 'docs/adr', undefined, {
        baselineSetting: '.adrguard-baseline.json',
      });
      assert.equal(failed.report.valid, false);
      assert.match(vscode.languages.getDiagnostics(uri)[0]?.message ?? '', /New since baseline/u);
    } finally {
      manager.dispose();
    }
  });

  test('does not turn operational failure or cancellation into a clean validation', async () => {
    const folder = vscode.workspace.workspaceFolders?.[0];
    assert.ok(folder);
    const fake = new FakeExecutor(folder.uri.fsPath);
    const manager = new DiagnosticManager(
      fake as unknown as CliExecutor,
      { info: () => undefined } as unknown as OperationalLog,
    );
    try {
      fake.exitCode = 3;
      await assert.rejects(manager.validate(folder, 'docs/adr'), /exit code 3/u);
      fake.termination = 'cancelled';
      await assert.rejects(manager.validate(folder, 'docs/adr'), (error: unknown) =>
        error instanceof DOMException && error.name === 'AbortError');
    } finally {
      manager.dispose();
    }
  });

  test('preserves workspace diagnostics when an editor closes', async () => {
    const uri = vscode.Uri.parse('adr-guard-test:/close-lifecycle.md');
    const deleted: string[] = [];
    const diagnostics = {
      delete: (target: vscode.Uri) => deleted.push(target.toString()),
    } as unknown as DiagnosticManager;
    const controller = new SaveValidationController(
      diagnostics,
      { info: () => undefined } as unknown as OperationalLog,
    );
    const provider = vscode.workspace.registerTextDocumentContentProvider('adr-guard-test', {
      provideTextDocumentContent: () => '# Temporary ADR',
    });
    try {
      const document = await vscode.workspace.openTextDocument(uri);
      await vscode.window.showTextDocument(document);
      await vscode.commands.executeCommand('workbench.action.closeActiveEditor');
      await new Promise((resolve) => setTimeout(resolve, 100));
      assert.deepEqual(deleted, []);
    } finally {
      controller.dispose();
      provider.dispose();
    }
  });
});

class FakeExecutor {
  public report = invalidReport();
  public exitCode = 1;
  public termination: CliRunResult['termination'] = 'exited';
  public lastArguments: readonly string[] = [];

  public constructor(private readonly root: string) {}

  public execute(_folder: vscode.WorkspaceFolder, args: readonly string[]): Promise<{ result: CliRunResult; workspaceRoot: string }> {
    this.lastArguments = args;
    return Promise.resolve({
      workspaceRoot: this.root,
      result: {
        termination: this.termination,
        exitCode: this.exitCode,
        exitKind: this.exitCode === 0 ? 'success' : 'validation-failed',
        stdout: JSON.stringify(this.report),
        stderr: '',
        signal: null,
      },
    });
  }
}

function pathLike(value: string): boolean {
  return /^[A-Za-z]:\\/u.test(value);
}

function invalidReport(): object {
  return {
    schemaVersion: '1.0',
    valid: false,
    summary: { files: 1, diagnostics: 1 },
    files: ['docs/adr/0001-invalid.md'],
    diagnostics: [{
      code: 'ADR005',
      message: "ADR must define a non-empty 'Decision' section.",
      file: 'docs/adr/0001-invalid.md',
    }],
  };
}

function validReport(): object {
  return {
    schemaVersion: '1.0',
    valid: true,
    summary: { files: 1, diagnostics: 0 },
    files: ['docs/adr/0001-invalid.md'],
    diagnostics: [],
  };
}

function baselineReport(): object {
  return {
    schemaVersion: '1.0',
    valid: true,
    summary: { files: 1, diagnostics: 1 },
    files: ['docs/adr/0001-invalid.md'],
    baseline: { new: 0, existing: 1, resolved: 2 },
    diagnostics: [{
      code: 'ADR005',
      message: "ADR must define a non-empty 'Decision' section.",
      file: 'docs/adr/0001-invalid.md',
      baselineState: 'existing',
    }],
  };
}

function newBaselineReport(): object {
  return {
    schemaVersion: '1.0',
    valid: false,
    summary: { files: 1, diagnostics: 1 },
    files: ['docs/adr/0001-invalid.md'],
    baseline: { new: 1, existing: 0, resolved: 0 },
    diagnostics: [{
      code: 'ADR005',
      message: "ADR must define a non-empty 'Decision' section.",
      file: 'docs/adr/0001-invalid.md',
      baselineState: 'new',
    }],
  };
}
