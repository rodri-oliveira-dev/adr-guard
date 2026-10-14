import { lstat, realpath, stat } from 'node:fs/promises';
import path from 'node:path';

export interface WorkspaceLocation {
  readonly name: string;
  readonly scheme: string;
  readonly fsPath: string;
}

export class WorkspacePolicyError extends Error {
  public constructor(
    message: string,
    public readonly code: 'untrusted' | 'missing' | 'non-file' | 'invalid-path' | 'outside-workspace',
  ) {
    super(message);
    this.name = 'WorkspacePolicyError';
  }
}

export function eligibleWorkspaceFolders(
  trusted: boolean,
  folders: readonly WorkspaceLocation[] | undefined,
): readonly WorkspaceLocation[] {
  if (!trusted) {
    throw new WorkspacePolicyError(
      'Trust this workspace before running the ADR Guard CLI.',
      'untrusted',
    );
  }
  if (folders === undefined || folders.length === 0) {
    throw new WorkspacePolicyError(
      'Open a local folder or workspace before running ADR Guard.',
      'missing',
    );
  }
  const localFolders = folders.filter((folder) => folder.scheme === 'file');
  if (localFolders.length === 0) {
    throw new WorkspacePolicyError(
      'ADR Guard requires a local file-system workspace; virtual workspace resources are unsupported.',
      'non-file',
    );
  }
  return localFolders;
}

export function preferredWorkspaceLocation(
  eligible: readonly WorkspaceLocation[],
  activeFsPath: string | undefined,
): WorkspaceLocation | undefined {
  if (eligible.length === 1) return eligible[0];
  if (activeFsPath === undefined) return undefined;
  return eligible.find((folder) => folder.fsPath === activeFsPath);
}

export async function resolveWorkspaceRoot(folder: WorkspaceLocation): Promise<string> {
  if (folder.scheme !== 'file' || !path.isAbsolute(folder.fsPath)) {
    throw new WorkspacePolicyError('The selected workspace folder is not a valid local path.', 'invalid-path');
  }
  try {
    const resolved = await realpath(folder.fsPath);
    if (!(await stat(resolved)).isDirectory()) {
      throw new Error('workspace path is not a directory');
    }
    return resolved;
  } catch (error: unknown) {
    const detail = error instanceof Error ? error.message : 'unknown filesystem error';
    throw new WorkspacePolicyError(`The workspace folder is inaccessible: ${detail}`, 'invalid-path');
  }
}

export async function ensureResourceWithinWorkspace(
  resourcePath: string,
  workspaceRoot: string,
): Promise<string> {
  try {
    const [resource, root] = await Promise.all([realpath(resourcePath), realpath(workspaceRoot)]);
    const relative = path.relative(normalizeForPlatform(root), normalizeForPlatform(resource));
    if (relative === '..' || relative.startsWith(`..${path.sep}`) || path.isAbsolute(relative)) {
      throw new WorkspacePolicyError(
        'The requested resource resolves outside the selected workspace folder.',
        'outside-workspace',
      );
    }
    return resource;
  } catch (error: unknown) {
    if (error instanceof WorkspacePolicyError) {
      throw error;
    }
    const detail = error instanceof Error ? error.message : 'unknown filesystem error';
    throw new WorkspacePolicyError(`The requested resource is inaccessible: ${detail}`, 'invalid-path');
  }
}

export async function ensurePotentialDirectoryWithinWorkspace(
  directoryPath: string,
  workspaceRoot: string,
): Promise<string> {
  let resolvedRoot: string;
  try {
    resolvedRoot = await realpath(workspaceRoot);
  } catch (error: unknown) {
    const detail = error instanceof Error ? error.message : 'unknown filesystem error';
    throw new WorkspacePolicyError(`The workspace folder is inaccessible: ${detail}`, 'invalid-path');
  }
  let cursor = directoryPath;
  for (;;) {
    try {
      const resolved = await realpath(cursor);
      if (!isResolvedPathWithin(resolved, resolvedRoot)) {
        throw new WorkspacePolicyError(
          'The requested directory resolves outside the selected workspace folder.',
          'outside-workspace',
        );
      }
      if (cursor === directoryPath && !(await stat(resolved)).isDirectory()) {
        throw new WorkspacePolicyError('The requested ADR directory is not a directory.', 'invalid-path');
      }
      return directoryPath;
    } catch (error: unknown) {
      if (error instanceof WorkspacePolicyError) throw error;
      if (isMissingPathError(error) && cursor !== workspaceRoot) {
        const parent = path.dirname(cursor);
        if (parent !== cursor) {
          cursor = parent;
          continue;
        }
      }
      const detail = error instanceof Error ? error.message : 'unknown filesystem error';
      throw new WorkspacePolicyError(`The requested directory is inaccessible: ${detail}`, 'invalid-path');
    }
  }
}

export async function ensurePotentialFileWithinWorkspace(
  filePath: string,
  workspaceRoot: string,
): Promise<string> {
  try {
    const fileStat = await lstat(filePath);
    if (fileStat.isSymbolicLink() || !fileStat.isFile()) {
      throw new WorkspacePolicyError('The requested file must be a regular, non-symbolic-link file.', 'invalid-path');
    }
    await ensureResourceWithinWorkspace(filePath, workspaceRoot);
    return filePath;
  } catch (error: unknown) {
    if (error instanceof WorkspacePolicyError) throw error;
    if (isMissingPathError(error)) {
      await ensurePotentialDirectoryWithinWorkspace(path.dirname(filePath), workspaceRoot);
      return filePath;
    }
    const detail = error instanceof Error ? error.message : 'unknown filesystem error';
    throw new WorkspacePolicyError(`The requested file is inaccessible: ${detail}`, 'invalid-path');
  }
}

function normalizeForPlatform(value: string): string {
  const resolved = path.resolve(value);
  return process.platform === 'win32' ? resolved.toLowerCase() : resolved;
}

function isMissingPathError(error: unknown): error is NodeJS.ErrnoException {
  return error instanceof Error && 'code' in error && error.code === 'ENOENT';
}

export function isResolvedPathWithin(candidate: string, root: string): boolean {
  const relative = path.relative(normalizeForPlatform(root), normalizeForPlatform(candidate));
  return relative === ''
    || (relative !== '..' && !relative.startsWith(`..${path.sep}`) && !path.isAbsolute(relative));
}

export function resolveWorkspaceRelativePath(workspaceRoot: string, relativePath: string): string {
  if (relativePath.trim().length === 0 || path.isAbsolute(relativePath) || relativePath.includes('\0')) {
    throw new WorkspacePolicyError(
      'The ADR directory must be a relative path inside the workspace.',
      'invalid-path',
    );
  }
  const candidate = path.resolve(workspaceRoot, relativePath);
  if (!isResolvedPathWithin(candidate, workspaceRoot)) {
    throw new WorkspacePolicyError(
      'The ADR directory must be a relative path inside the workspace.',
      'outside-workspace',
    );
  }
  return candidate;
}
