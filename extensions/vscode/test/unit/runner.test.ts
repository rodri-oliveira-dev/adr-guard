import assert from 'node:assert/strict';
import path from 'node:path';
import { describe, it } from 'node:test';
import { CliRunner } from '../../src/cli/runner';

const fixture = path.resolve(__dirname, '../../../test/fixtures/cli-fixture.cjs');
const cwd = path.resolve(__dirname);

function request(args: readonly string[], overrides: { timeoutMilliseconds?: number; maxOutputBytes?: number; signal?: AbortSignal; input?: string; maxInputCharacters?: number } = {}) {
  return {
    executable: process.execPath,
    args: [fixture, ...args],
    cwd,
    timeoutMilliseconds: overrides.timeoutMilliseconds ?? 5000,
    maxOutputBytes: overrides.maxOutputBytes ?? 4096,
    ...(overrides.signal ? { signal: overrides.signal } : {}),
    ...(overrides.input === undefined ? {} : { input: overrides.input }),
    ...(overrides.maxInputCharacters === undefined ? {} : { maxInputCharacters: overrides.maxInputCharacters }),
  };
}

describe('CliRunner', () => {
  it('passes special arguments literally without a shell', async () => {
    const runner = new CliRunner();
    const special = ['a b', '$(not-a-command)', 'x; echo injected', '"quoted"', '&whoami'];
    const result = await runner.run(request(['args', ...special]));
    assert.equal(result.termination, 'exited');
    assert.equal(result.exitCode, 0);
    assert.deepEqual(JSON.parse(result.stdout), special);
  });

  it('normalizes documented and unknown nonzero exits', async () => {
    const runner = new CliRunner();
    for (const [code, kind] of [[1, 'validation-failed'], [2, 'usage-error'], [3, 'operational-error'], [4, 'policy-failed'], [17, 'unknown']] as const) {
      const result = await runner.run(request(['exit', String(code)]));
      assert.equal(result.exitCode, code);
      assert.equal(result.exitKind, kind);
      assert.equal(result.stderr, 'controlled failure');
    }
  });

  it('writes bounded structured input to stdin without adding arguments', async () => {
    const runner = new CliRunner();
    const input = '{"schemaVersion":"1.0","title":"Decisão com acentos"}';
    const result = await runner.run(request(['stdin'], { input }));
    assert.equal(result.termination, 'exited');
    assert.equal(result.exitCode, 0);
    assert.equal(result.stdout, input);
  });

  it('rejects oversized stdin before spawning', async () => {
    const runner = new CliRunner();
    await assert.rejects(
      runner.run(request(['stdin'], { input: 'x'.repeat(131073) })),
      /input exceeds/iu,
    );
  });

  it('accepts the larger bounded edit form contract', async () => {
    const runner = new CliRunner();
    const input = 'x'.repeat(131073);
    const result = await runner.run(request(['stdin'], {
      input,
      maxInputCharacters: 384 * 1024,
      maxOutputBytes: 384 * 1024,
    }));
    assert.equal(result.exitCode, 0);
    assert.equal(result.stdout, input);
  });

  it('applies the form limit to characters instead of UTF-8 bytes', async () => {
    const runner = new CliRunner();
    const input = 'á'.repeat(80_000);
    const result = await runner.run(request(['stdin'], { input, maxOutputBytes: 200_000 }));
    assert.equal(result.exitCode, 0);
    assert.equal(result.stdout, input);
  });

  it('terminates timed-out processes', async () => {
    const runner = new CliRunner();
    const result = await runner.run(request(['wait'], { timeoutMilliseconds: 50 }));
    assert.equal(result.termination, 'timed-out');
  });

  it('force-kills a process that ignores graceful termination', { skip: process.platform === 'win32' }, async () => {
    const runner = new CliRunner();
    const started = Date.now();
    const result = await runner.run(request(['ignore-term'], { timeoutMilliseconds: 50 }));
    assert.equal(result.termination, 'timed-out');
    assert.ok(Date.now() - started < 3000, 'forced termination must remain bounded');
  });

  it('terminates descendants that inherit the CLI output pipes', async () => {
    const runner = new CliRunner();
    const started = Date.now();
    const result = await runner.run(request(['process-tree'], { timeoutMilliseconds: 50 }));
    assert.equal(result.termination, 'timed-out');
    assert.ok(Date.now() - started < 3000, 'process-tree termination must remain bounded');
  });

  it('terminates cancelled processes', async () => {
    const runner = new CliRunner();
    const controller = new AbortController();
    setTimeout(() => controller.abort(), 50);
    const result = await runner.run(request(['wait'], { signal: controller.signal }));
    assert.equal(result.termination, 'cancelled');
  });

  it('terminates when stdout exceeds its byte limit', async () => {
    const runner = new CliRunner();
    const result = await runner.run(request(['output', '8192'], { maxOutputBytes: 128 }));
    assert.equal(result.termination, 'output-limit');
    assert.equal(Buffer.byteLength(result.stdout), 128);
  });

  it('terminates when stderr exceeds its byte limit', async () => {
    const runner = new CliRunner();
    const result = await runner.run(request(['error-output', '8192'], { maxOutputBytes: 128 }));
    assert.equal(result.termination, 'output-limit');
    assert.equal(Buffer.byteLength(result.stderr), 128);
  });

  it('cancels active processes on dispose', async () => {
    const runner = new CliRunner();
    const pending = runner.run(request(['wait']));
    setTimeout(() => runner.dispose(), 50);
    assert.equal((await pending).termination, 'cancelled');
  });

  it('reports inaccessible executables as controlled spawn failures', async () => {
    const runner = new CliRunner();
    const result = await runner.run({ ...request([]), executable: path.join(cwd, 'missing-executable') });
    assert.equal(result.termination, 'spawn-error');
    assert.match(result.error ?? '', /ENOENT|not found|cannot find/iu);
  });
});
