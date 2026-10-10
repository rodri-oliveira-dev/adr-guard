using AdrGuard.Git;
using AdrGuard.Impact;

namespace AdrGuard.Cli;

internal enum ImpactOutputFormat
{
    Text,
    Json,
}

internal static class ImpactCommand
{
    internal static int Run(
        string repositoryPath,
        string baseReference,
        string manifestPath,
        ImpactOutputFormat format,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var inventory = GitChangeInventoryReader.Read(
                repositoryPath,
                baseReference,
                cancellationToken);
            var manifest = ImpactManifestLoader.Load(
                inventory.RepositoryRoot,
                manifestPath);
            var report = ImpactReportFactory.Create(
                ImpactCorrelationService.Analyze(manifest, inventory));

            if (format == ImpactOutputFormat.Json)
            {
                ImpactReportRenderer.WriteJson(report, output);
            }
            else
            {
                ImpactReportRenderer.WriteText(report, output);
            }

            return ExitCodes.Success;
        }
        catch (Exception exception) when (exception is
            IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or InvalidOperationException
            or GitOperationException
            or ImpactManifestException
            or ImpactAnalysisException)
        {
            error.WriteLine($"Unable to analyze architecture impact: {exception.Message}");
            return ExitCodes.OperationalError;
        }
        catch (OperationCanceledException)
        {
            error.WriteLine("Architecture impact analysis was canceled.");
            return ExitCodes.OperationalError;
        }
    }
}
