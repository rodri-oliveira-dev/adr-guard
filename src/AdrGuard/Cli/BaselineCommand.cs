using AdrGuard.Baselines;
using AdrGuard.Parsing;
using AdrGuard.Validation;

namespace AdrGuard.Cli;

internal static class BaselineCommand
{
    internal static int Run(
        string directoryPath,
        string outputPath,
        bool update,
        AdrFormat format,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken) =>
        Run(directoryPath, outputPath, update, format, null, false, null, null, output, error, cancellationToken);

    internal static int Run(
        string directoryPath,
        string outputPath,
        bool update,
        AdrFormat format,
        string? lifecycleStatuses,
        bool conventionalSupersession,
        string? filenamePolicy,
        string? validationProfile,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directoryPath))
        {
            error.WriteLine($"ADR directory does not exist: '{directoryPath}'.");
            return ExitCodes.OperationalError;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var documents = AdrDocumentLoader.LoadDirectory(directoryPath, cancellationToken);
            var result = AdrValidator.Validate(
                documents,
                null,
                AdrValidationOptionsFactory.Create(
                    format,
                    AdrRepositoryRoot.Resolve(directoryPath),
                    lifecycleStatuses,
                    conventionalSupersession,
                    filenamePolicy,
                    null,
                    false,
                    validationProfile));
            var baseline = DiagnosticBaselineService.Create(result, directoryPath);
            var written = DiagnosticBaselineService.Write(outputPath, baseline, update);
            output.WriteLine(
                $"Diagnostic baseline written: {written} ({baseline.Diagnostics.Count} issue(s)).");
            return ExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            error.WriteLine("Diagnostic baseline generation was canceled.");
            return ExitCodes.OperationalError;
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or InvalidOperationException)
        {
            error.WriteLine($"Unable to generate diagnostic baseline: {exception.Message}");
            return ExitCodes.OperationalError;
        }
    }
}
