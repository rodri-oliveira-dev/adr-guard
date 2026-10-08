export const CLI_EXIT_CODES = {
  success: 0,
  validationFailed: 1,
  usageError: 2,
  operationalError: 3,
  policyFailed: 4,
} as const;

export type CliExitKind =
  | 'success'
  | 'validation-failed'
  | 'usage-error'
  | 'operational-error'
  | 'policy-failed'
  | 'unknown';

export function classifyExitCode(exitCode: number): CliExitKind {
  switch (exitCode) {
    case CLI_EXIT_CODES.success:
      return 'success';
    case CLI_EXIT_CODES.validationFailed:
      return 'validation-failed';
    case CLI_EXIT_CODES.usageError:
      return 'usage-error';
    case CLI_EXIT_CODES.operationalError:
      return 'operational-error';
    case CLI_EXIT_CODES.policyFailed:
      return 'policy-failed';
    default:
      return 'unknown';
  }
}
