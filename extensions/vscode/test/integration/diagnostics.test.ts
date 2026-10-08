import assert from 'node:assert/strict';
import * as vscode from 'vscode';
import type { CliExecutor } from '../../src/cli/executor';
import type { CliRunResult } from '../../src/cli/runner';
import type { OperationalLog } from '../../src/logging';
import { DiagnosticManager } from '../../src/validation/diagnostics';

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
});

class FakeExecutor {
  public report = invalidReport();
  public exitCode = 1;
  public lastArguments: readonly string[] = [];

  public constructor(private readonly root: string) {}

  public execute(_folder: vscode.WorkspaceFolder, args: readonly string[]): Promise<{ result: CliRunResult; workspaceRoot: string }> {
    this.lastArguments = args;
    return Promise.resolve({
      workspaceRoot: this.root,
      result: {
        termination: 'exited',
        exitCode: this.exitCode,
        exitKind: this.exitCode === 0 ? 'success' : 'validation-failed',
        stdout: JSON.stringify(this.report),
        stderr: '',
        signal: null,
      },
    });
  }
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
