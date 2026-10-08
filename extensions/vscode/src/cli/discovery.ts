import { constants as fsConstants } from 'node:fs';
import { access, realpath, stat } from 'node:fs/promises';
import path from 'node:path';

export type CliDiscoverySource = 'configured' | 'path';

export interface CliExecutable {
  readonly path: string;
  readonly source: CliDiscoverySource;
}

export interface CliDiscoveryOptions {
  readonly configuredPath?: string;
  readonly pathEnvironment?: string;
  readonly platform?: NodeJS.Platform;
  readonly workspaceRoots?: readonly string[];
}

export class CliDiscoveryError extends Error {
  public constructor(
    message: string,
    public readonly code: 'invalid-configured-path' | 'not-found',
  ) {
    super(message);
    this.name = 'CliDiscoveryError';
  }
}

interface FileSystemOperations {
  readonly access: typeof access;
  readonly realpath: typeof realpath;
  readonly stat: typeof stat;
}

const defaultFileSystem: FileSystemOperations = { access, realpath, stat };

export class CliDiscovery {
  public constructor(private readonly fileSystem: FileSystemOperations = defaultFileSystem) {}

  public async find(options: CliDiscoveryOptions): Promise<CliExecutable> {
    const configuredPath = options.configuredPath?.trim();
    if (configuredPath) {
      const platform = options.platform ?? process.platform;
      if (!isLocalAbsolutePath(configuredPath, platform) || configuredPath.includes('\0')) {
        throw new CliDiscoveryError(
          'The configured ADR Guard executable path must be an absolute local path.',
          'invalid-configured-path',
        );
      }

      try {
        return {
          path: await this.validateExecutable(configuredPath, options.platform),
          source: 'configured',
        };
      } catch (error: unknown) {
        throw new CliDiscoveryError(
          `The configured ADR Guard executable is unavailable or inaccessible: ${errorMessage(error)}`,
          'invalid-configured-path',
        );
      }
    }

    const platform = options.platform ?? process.platform;
    const delimiter = platform === 'win32' ? ';' : ':';
    const executableNames = platform === 'win32' ? ['adr-guard.exe'] : ['adr-guard'];
    const workspaceRoots = await this.resolveExistingPaths(options.workspaceRoots ?? []);
    const entries = (options.pathEnvironment ?? process.env.PATH ?? '')
      .split(delimiter)
      .map((entry) => entry.trim())
      .filter((entry) => entry.length > 0 && isLocalAbsolutePath(entry, platform));

    for (const entry of entries) {
      for (const executableName of executableNames) {
        try {
          const candidate = await this.validateExecutable(path.join(entry, executableName), platform);
          if (!workspaceRoots.some((root) => isPathWithin(candidate, root, platform))) {
            return { path: candidate, source: 'path' };
          }
        } catch {
          // Missing or inaccessible PATH entries are expected during discovery.
        }
      }
    }

    throw new CliDiscoveryError(
      'ADR Guard was not found. Configure adrGuard.cli.path with a trusted absolute executable path or install the CLI on the workspace extension host PATH.',
      'not-found',
    );
  }

  private async validateExecutable(candidate: string, platform = process.platform): Promise<string> {
    const resolved = await this.fileSystem.realpath(candidate);
    if (!path.isAbsolute(resolved)) {
      throw new Error('resolved path is not absolute');
    }

    const candidateStat = await this.fileSystem.stat(resolved);
    if (!candidateStat.isFile()) {
      throw new Error('path does not identify a regular file');
    }

    await this.fileSystem.access(
      resolved,
      platform === 'win32' ? fsConstants.F_OK : fsConstants.X_OK,
    );
    return resolved;
  }

  private async resolveExistingPaths(paths: readonly string[]): Promise<string[]> {
    const resolved = await Promise.all(
      paths.map(async (candidate) => {
        try {
          return await this.fileSystem.realpath(candidate);
        } catch {
          return undefined;
        }
      }),
    );
    return resolved.filter((candidate): candidate is string => candidate !== undefined);
  }
}

function isPathWithin(candidate: string, root: string, platform: NodeJS.Platform): boolean {
  const normalize = (value: string): string =>
    platform === 'win32' ? path.resolve(value).toLowerCase() : path.resolve(value);
  const relative = path.relative(normalize(root), normalize(candidate));
  return relative === '' || (!relative.startsWith(`..${path.sep}`) && relative !== '..' && !path.isAbsolute(relative));
}

function isLocalAbsolutePath(candidate: string, platform: NodeJS.Platform): boolean {
  if (platform === 'win32') {
    return path.win32.isAbsolute(candidate) && !candidate.startsWith('\\\\');
  }
  return path.posix.isAbsolute(candidate);
}

function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : 'unknown filesystem error';
}
