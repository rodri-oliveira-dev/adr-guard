import { constants as fsConstants } from 'node:fs';
import { access, lstat, realpath, stat } from 'node:fs/promises';
import path from 'node:path';

export type McpAvailabilityCode =
  | 'available'
  | 'not-configured'
  | 'not-found'
  | 'invalid-configuration'
  | 'cancelled';

export interface McpLaunch {
  readonly command: string;
  readonly argsPrefix: readonly string[];
  readonly serverPath: string;
}

export interface McpDiscoveryResult {
  readonly code: McpAvailabilityCode;
  readonly launch?: McpLaunch;
  readonly detail?: string;
}

interface FileSystemOperations {
  readonly access: typeof access;
  readonly lstat: typeof lstat;
  readonly realpath: typeof realpath;
  readonly stat: typeof stat;
}

const defaultFileSystem: FileSystemOperations = { access, lstat, realpath, stat };

export interface McpDiscoveryOptions {
  readonly configuredPath?: string;
  readonly workspaceRoots: readonly string[];
  readonly pathEnvironment?: string;
  readonly platform?: NodeJS.Platform;
  readonly signal?: AbortSignal;
}

export class McpServerDiscovery {
  public constructor(private readonly fileSystem: FileSystemOperations = defaultFileSystem) {}

  public async inspect(options: McpDiscoveryOptions): Promise<McpDiscoveryResult> {
    const configuredPath = options.configuredPath?.trim();
    if (!configuredPath) return { code: 'not-configured' };
    if (isCancelled(options.signal)) return { code: 'cancelled' };
    const platform = options.platform ?? process.platform;
    if (!isSafeMcpServerPath(configuredPath, platform)) {
      return { code: 'invalid-configuration', detail: 'The MCP server path must be an absolute local path.' };
    }

    try {
      const originalStat = await this.fileSystem.lstat(configuredPath);
      if (originalStat.isSymbolicLink() || !originalStat.isFile()) {
        return { code: 'invalid-configuration', detail: 'The MCP server path must be a regular, non-symbolic-link file.' };
      }
      const serverPath = await this.fileSystem.realpath(configuredPath);
      const resolvedRoots = await this.resolveExistingPaths(options.workspaceRoots);
      if (resolvedRoots.some((root) => isPathWithin(serverPath, root, platform))) {
        return { code: 'invalid-configuration', detail: 'The MCP server must not be loaded from the workspace.' };
      }
      if (isCancelled(options.signal)) return { code: 'cancelled' };

      const basename = path.basename(serverPath).toLowerCase();
      if (basename === 'adr-guard-mcp.dll') {
        const runtime = await this.findRuntime(options.pathEnvironment, platform, resolvedRoots, options.signal);
        if (isCancelled(options.signal)) return { code: 'cancelled' };
        if (runtime === undefined) {
          return { code: 'not-found', detail: 'The dotnet runtime was not found on the extension host PATH.' };
        }
        return { code: 'available', launch: { command: runtime, argsPrefix: [serverPath], serverPath } };
      }

      const expectedName = platform === 'win32' ? 'adr-guard-mcp.exe' : 'adr-guard-mcp';
      if (basename !== expectedName) {
        return { code: 'invalid-configuration', detail: `Expected adr-guard-mcp.dll or ${expectedName}.` };
      }
      await this.fileSystem.access(serverPath, platform === 'win32' ? fsConstants.F_OK : fsConstants.X_OK);
      return { code: 'available', launch: { command: serverPath, argsPrefix: [], serverPath } };
    } catch (error: unknown) {
      if (isCancelled(options.signal)) return { code: 'cancelled' };
      const code = isNodeError(error) ? error.code : undefined;
      return {
        code: code === 'ENOENT' ? 'not-found' : 'invalid-configuration',
        detail: error instanceof Error ? error.message : 'unknown filesystem error',
      };
    }
  }

  private async findRuntime(
    pathEnvironment: string | undefined,
    platform: NodeJS.Platform,
    workspaceRoots: readonly string[],
    signal: AbortSignal | undefined,
  ): Promise<string | undefined> {
    const delimiter = platform === 'win32' ? ';' : ':';
    const names = platform === 'win32' ? ['dotnet.exe'] : ['dotnet'];
    const entries = (pathEnvironment ?? process.env.PATH ?? '')
      .split(delimiter)
      .map((entry) => entry.trim())
      .filter((entry) => entry.length > 0 && isSafeMcpServerPath(entry, platform));
    for (const entry of entries.slice(0, 256)) {
      if (isCancelled(signal)) return undefined;
      for (const name of names) {
        try {
          const candidate = await this.fileSystem.realpath(path.join(entry, name));
          if (workspaceRoots.some((root) => isPathWithin(candidate, root, platform))) continue;
          if (!(await this.fileSystem.stat(candidate)).isFile()) continue;
          await this.fileSystem.access(candidate, platform === 'win32' ? fsConstants.F_OK : fsConstants.X_OK);
          return candidate;
        } catch {
          // Missing and inaccessible PATH entries are expected.
        }
      }
    }
    return undefined;
  }

  private async resolveExistingPaths(paths: readonly string[]): Promise<string[]> {
    const values = await Promise.all(paths.map(async (candidate) => {
      try { return await this.fileSystem.realpath(candidate); } catch { return undefined; }
    }));
    return values.filter((value): value is string => value !== undefined);
  }
}

export function isSafeMcpServerPath(candidate: string, platform: NodeJS.Platform = process.platform): boolean {
  if (candidate.includes('\0')) return false;
  if (platform === 'win32') return path.win32.isAbsolute(candidate) && !candidate.startsWith('\\\\');
  return path.posix.isAbsolute(candidate);
}

function isPathWithin(candidate: string, root: string, platform: NodeJS.Platform): boolean {
  const normalize = (value: string): string => platform === 'win32'
    ? path.resolve(value).toLowerCase()
    : path.resolve(value);
  const relative = path.relative(normalize(root), normalize(candidate));
  return relative === '' || (relative !== '..' && !relative.startsWith(`..${path.sep}`) && !path.isAbsolute(relative));
}

function isNodeError(error: unknown): error is NodeJS.ErrnoException {
  return error instanceof Error && 'code' in error;
}

function isCancelled(signal: AbortSignal | undefined): boolean {
  return signal?.aborted ?? false;
}
