import { spawn, type ChildProcess } from 'node:child_process';
import path from 'node:path';
import { classifyExitCode, type CliExitKind } from '../contracts/cli';

export interface CliRunRequest {
  readonly executable: string;
  readonly args: readonly string[];
  readonly cwd: string;
  readonly timeoutMilliseconds: number;
  readonly maxOutputBytes: number;
  readonly signal?: AbortSignal;
}

export type CliTermination = 'exited' | 'cancelled' | 'timed-out' | 'output-limit' | 'spawn-error';

export interface CliRunResult {
  readonly termination: CliTermination;
  readonly exitCode: number | null;
  readonly exitKind: CliExitKind | null;
  readonly stdout: string;
  readonly stderr: string;
  readonly signal: NodeJS.Signals | null;
  readonly error?: string;
}

type SpawnFunction = typeof spawn;

export class CliRunner {
  private readonly activeControllers = new Set<AbortController>();
  private disposed = false;

  public constructor(private readonly spawnProcess: SpawnFunction = spawn) {}

  public async run(request: CliRunRequest): Promise<CliRunResult> {
    this.validateRequest(request);
    if (this.disposed || request.signal?.aborted === true) {
      return emptyResult('cancelled');
    }

    const controller = new AbortController();
    this.activeControllers.add(controller);
    const cancel = (): void => controller.abort();
    request.signal?.addEventListener('abort', cancel, { once: true });

    try {
      return await this.runProcess(request, controller.signal);
    } finally {
      request.signal?.removeEventListener('abort', cancel);
      this.activeControllers.delete(controller);
    }
  }

  public dispose(): void {
    this.disposed = true;
    for (const controller of this.activeControllers) {
      controller.abort();
    }
    this.activeControllers.clear();
  }

  private runProcess(request: CliRunRequest, signal: AbortSignal): Promise<CliRunResult> {
    return new Promise((resolve) => {
      let stdout: Buffer = Buffer.alloc(0);
      let stderr: Buffer = Buffer.alloc(0);
      let termination: CliTermination = 'exited';
      let spawnError: string | undefined;
      let settled = false;

      const child = this.spawnProcess(request.executable, [...request.args], {
        cwd: request.cwd,
        shell: false,
        windowsHide: true,
        stdio: ['ignore', 'pipe', 'pipe'],
      });

      const terminate = (reason: Exclude<CliTermination, 'exited' | 'spawn-error'>): void => {
        if (termination !== 'exited') {
          return;
        }
        termination = reason;
        safelyKill(child);
      };

      const timeout = setTimeout(() => terminate('timed-out'), request.timeoutMilliseconds);
      const onAbort = (): void => terminate('cancelled');
      signal.addEventListener('abort', onAbort, { once: true });

      child.stdout.on('data', (chunk: Buffer) => {
        const append = appendBounded(stdout, chunk, request.maxOutputBytes);
        stdout = append.value;
        if (append.exceeded) {
          terminate('output-limit');
        }
      });
      child.stderr.on('data', (chunk: Buffer) => {
        const append = appendBounded(stderr, chunk, request.maxOutputBytes);
        stderr = append.value;
        if (append.exceeded) {
          terminate('output-limit');
        }
      });
      child.once('error', (error) => {
        termination = 'spawn-error';
        spawnError = error.message;
      });
      child.once('close', (exitCode, exitSignal) => {
        if (settled) {
          return;
        }
        settled = true;
        clearTimeout(timeout);
        signal.removeEventListener('abort', onAbort);
        const normalizedExitCode = typeof exitCode === 'number' ? exitCode : null;
        const base = {
          termination,
          exitCode: normalizedExitCode,
          exitKind: normalizedExitCode === null ? null : classifyExitCode(normalizedExitCode),
          stdout: stdout.toString('utf8'),
          stderr: stderr.toString('utf8'),
          signal: exitSignal,
        };
        resolve(spawnError === undefined ? base : { ...base, error: spawnError });
      });
    });
  }

  private validateRequest(request: CliRunRequest): void {
    if (!path.isAbsolute(request.executable) || !path.isAbsolute(request.cwd)) {
      throw new TypeError('CLI executable and working directory must be absolute paths.');
    }
    if (request.args.some((argument) => argument.includes('\0'))) {
      throw new TypeError('CLI arguments must not contain NUL characters.');
    }
    if (!Number.isInteger(request.timeoutMilliseconds) || request.timeoutMilliseconds < 1) {
      throw new RangeError('CLI timeout must be a positive integer.');
    }
    if (!Number.isInteger(request.maxOutputBytes) || request.maxOutputBytes < 1) {
      throw new RangeError('CLI output limit must be a positive integer.');
    }
  }
}

function appendBounded(current: Buffer, chunk: Buffer, limit: number): { value: Buffer; exceeded: boolean } {
  const available = Math.max(0, limit - current.length);
  return {
    value: available === 0 ? current : Buffer.concat([current, chunk.subarray(0, available)]),
    exceeded: chunk.length > available,
  };
}

function safelyKill(child: ChildProcess): void {
  if (child.exitCode !== null || child.signalCode !== null) {
    return;
  }
  try {
    child.kill('SIGTERM');
  } catch {
    // The close/error event still determines the controlled result.
  }
}

function emptyResult(termination: CliTermination): CliRunResult {
  return {
    termination,
    exitCode: null,
    exitKind: null,
    stdout: '',
    stderr: '',
    signal: null,
  };
}
