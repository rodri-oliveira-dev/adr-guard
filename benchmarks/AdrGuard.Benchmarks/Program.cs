using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AdrGuard.Benchmarks;

internal static class Program
{
    private static readonly int[] ScenarioSizes = [100, 500, 900];
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(2);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private const int MaximumCapturedCharacters = 2 * 1024 * 1024;

    internal static async Task<int> Main(string[] args)
    {
        try
        {
            var options = BenchmarkOptions.Parse(args);
            var report = await RunAsync(options);
            var outputPath = Path.GetFullPath(options.OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await File.WriteAllTextAsync(
                outputPath,
                JsonSerializer.Serialize(report, JsonOptions),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Console.WriteLine($"Wrote reproducible benchmark report to {outputPath}");
            return 0;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static async Task<BenchmarkReport> RunAsync(BenchmarkOptions options)
    {
        var cliAssembly = Path.GetFullPath(options.CliAssembly);
        if (!File.Exists(cliAssembly))
        {
            throw new FileNotFoundException("The Release-built ADR Guard CLI assembly was not found.", cliAssembly);
        }

        var root = Path.Combine(
            Path.GetTempPath(),
            $"adr-guard-benchmarks-{Guid.NewGuid():N}");
        var results = new List<ScenarioResult>();
        try
        {
            Directory.CreateDirectory(root);
            foreach (var size in ScenarioSizes)
            {
                var scenarioRoot = Path.Combine(root, size.ToString(CultureInfo.InvariantCulture));
                var validDirectory = Path.Combine(scenarioRoot, "valid");
                var invalidDirectory = Path.Combine(scenarioRoot, "invalid");
                var preparation = Stopwatch.StartNew();
                SyntheticAdrGenerator.Generate(validDirectory, size, includeErrors: false);
                SyntheticAdrGenerator.Generate(invalidDirectory, size, includeErrors: true);
                preparation.Stop();

                var validHash = SyntheticAdrGenerator.ComputeHash(validDirectory);
                var invalidHash = SyntheticAdrGenerator.ComputeHash(invalidDirectory);
                results.Add(await MeasureAsync(
                    cliAssembly,
                    size,
                    "check-invalid",
                    invalidDirectory,
                    invalidHash,
                    preparation.Elapsed,
                    options.Samples,
                    expectedExitCode: 1));
                results.Add(await MeasureAsync(
                    cliAssembly,
                    size,
                    "index-valid",
                    validDirectory,
                    validHash,
                    preparation.Elapsed,
                    options.Samples,
                    expectedExitCode: 0));

                DeleteDirectory(scenarioRoot);
            }

            var version = await RunProcessAsync(
                "dotnet",
                [cliAssembly, "--version"],
                Directory.GetCurrentDirectory());
            EnsureExit("version", version, expectedExitCode: 0);
            var revision = await TryReadGitValueAsync("rev-parse", "HEAD") ?? "unknown";
            var status = await TryReadGitValueAsync("status", "--porcelain");

            return new BenchmarkReport(
                SchemaVersion: "1.0",
                RecordedAtUtc: DateTimeOffset.UtcNow,
                SourceRevision: revision,
                WorkingTreeDirty: status is null
                    ? null
                    : !string.IsNullOrWhiteSpace(status),
                CliVersion: version.Output.Trim(),
                SamplesPerScenario: options.Samples,
                Environment: new EnvironmentMetadata(
                    RuntimeInformation.OSDescription,
                    RuntimeInformation.OSArchitecture.ToString(),
                    RuntimeInformation.ProcessArchitecture.ToString(),
                    RuntimeInformation.FrameworkDescription,
                    Environment.ProcessorCount,
                    GC.GetGCMemoryInfo().TotalAvailableMemoryBytes),
                Results: results);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static async Task<ScenarioResult> MeasureAsync(
        string cliAssembly,
        int adrCount,
        string operation,
        string directory,
        string datasetHash,
        TimeSpan preparationTime,
        int samples,
        int expectedExitCode)
    {
        ProcessResult firstRun;
        var subsequent = new List<ProcessResult>(samples);
        if (operation == "index-valid")
        {
            DeleteIndex(directory);
            firstRun = await RunProcessAsync("dotnet", [cliAssembly, "index", directory], directory);
            EnsureExit(operation, firstRun, expectedExitCode);
            for (var sample = 0; sample < samples; sample++)
            {
                DeleteIndex(directory);
                var result = await RunProcessAsync("dotnet", [cliAssembly, "index", directory], directory);
                EnsureExit(operation, result, expectedExitCode);
                subsequent.Add(result);
            }
        }
        else
        {
            firstRun = await RunProcessAsync(
                "dotnet",
                [cliAssembly, "check", directory, "--format", "json"],
                directory);
            EnsureExit(operation, firstRun, expectedExitCode);
            using (var report = JsonDocument.Parse(firstRun.Output))
            {
                if (report.RootElement.GetProperty("valid").GetBoolean())
                {
                    throw new InvalidDataException("The synthetic invalid check unexpectedly passed.");
                }
            }

            for (var sample = 0; sample < samples; sample++)
            {
                var result = await RunProcessAsync(
                    "dotnet",
                    [cliAssembly, "check", directory, "--format", "json"],
                    directory);
                EnsureExit(operation, result, expectedExitCode);
                subsequent.Add(result);
            }
        }

        return new ScenarioResult(
            adrCount,
            operation,
            datasetHash,
            Math.Round(preparationTime.TotalMilliseconds, 3),
            ToSample(firstRun),
            Summarize(subsequent));
    }

    private static SampleResult ToSample(ProcessResult result) =>
        new(
            Math.Round(result.Elapsed.TotalMilliseconds, 3),
            result.PeakWorkingSetBytes,
            result.ExitCode);

    private static SampleSummary Summarize(IReadOnlyList<ProcessResult> results)
    {
        var elapsed = results
            .Select(result => result.Elapsed.TotalMilliseconds)
            .Order()
            .ToArray();
        var memory = results
            .Select(result => result.PeakWorkingSetBytes)
            .Order()
            .ToArray();
        return new SampleSummary(
            results.Count,
            Math.Round(Median(elapsed), 3),
            Math.Round(elapsed[0], 3),
            Math.Round(elapsed[^1], 3),
            Math.Round(elapsed[^1] - elapsed[0], 3),
            Convert.ToInt64(Median(memory)));
    }

    private static double Median(double[] values) =>
        values.Length % 2 == 0
            ? (values[values.Length / 2 - 1] + values[values.Length / 2]) / 2
            : values[values.Length / 2];

    private static double Median(long[] values) =>
        values.Length % 2 == 0
            ? (values[values.Length / 2 - 1] + values[values.Length / 2]) / 2d
            : values[values.Length / 2];

    private static async Task<ProcessResult> RunProcessAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Unable to start '{executable}'.");
        var stopwatch = Stopwatch.StartNew();
        using var timeout = new CancellationTokenSource(ProcessTimeout);
        var outputTask = ReadBoundedAsync(process.StandardOutput, timeout.Token);
        var errorTask = ReadBoundedAsync(process.StandardError, timeout.Token);
        long peakWorkingSetBytes = 0;
        try
        {
            var exitTask = process.WaitForExitAsync(timeout.Token);
            while (!exitTask.IsCompleted)
            {
                await ThrowIfCaptureFailedAsync(outputTask, errorTask);
                CapturePeakWorkingSet(process, ref peakWorkingSetBytes);

                await Task.WhenAny(
                    exitTask,
                    Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token));
            }
            await exitTask;
            CapturePeakWorkingSet(process, ref peakWorkingSetBytes);
            await ThrowIfCaptureFailedAsync(outputTask, errorTask);
        }
        catch (OperationCanceledException)
        {
            await TerminateAsync(process);
            throw new TimeoutException($"'{executable}' exceeded the {ProcessTimeout.TotalSeconds}-second benchmark timeout.");
        }
        catch (InvalidDataException)
        {
            await TerminateAsync(process);
            throw;
        }
        stopwatch.Stop();
        var output = await outputTask;
        var error = await errorTask;

        return new ProcessResult(
            process.ExitCode,
            output,
            error,
            stopwatch.Elapsed,
            peakWorkingSetBytes);
    }

    private static async Task<string> ReadBoundedAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        var output = new StringBuilder();
        var buffer = new char[4096];
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
            {
                return output.ToString();
            }

            if (output.Length > MaximumCapturedCharacters - read)
            {
                throw new InvalidDataException("Benchmark process output exceeded the capture limit.");
            }

            output.Append(buffer, 0, read);
        }
    }

    private static async Task ThrowIfCaptureFailedAsync(params Task<string>[] captureTasks)
    {
        foreach (var captureTask in captureTasks)
        {
            if (captureTask.IsFaulted || captureTask.IsCanceled)
            {
                await captureTask;
            }
        }
    }

    private static void CapturePeakWorkingSet(Process process, ref long peakWorkingSetBytes)
    {
        try
        {
            process.Refresh();
            peakWorkingSetBytes = Math.Max(peakWorkingSetBytes, process.PeakWorkingSet64);
        }
        catch (InvalidOperationException)
        {
            // The process handle became unavailable while capturing its final peak.
        }
    }

    private static async Task TerminateAsync(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited while termination was requested.
        }

        await process.WaitForExitAsync();
    }

    private static void EnsureExit(string operation, ProcessResult result, int expectedExitCode)
    {
        if (result.ExitCode != expectedExitCode)
        {
            throw new InvalidOperationException(
                $"{operation} returned {result.ExitCode}, expected {expectedExitCode}. "
                + $"stderr: {result.Error.Trim()}");
        }
    }

    private static async Task<string?> TryReadGitValueAsync(params string[] arguments)
    {
        try
        {
            var result = await RunProcessAsync("git", arguments, Directory.GetCurrentDirectory());
            return result.ExitCode == 0 ? result.Output.Trim() : null;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or TimeoutException or Win32Exception)
        {
            return null;
        }
    }

    private static void DeleteIndex(string directory)
    {
        var indexPath = Path.Combine(directory, "README.md");
        if (File.Exists(indexPath))
        {
            File.Delete(indexPath);
        }
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed record BenchmarkOptions(string CliAssembly, string OutputPath, int Samples)
    {
        internal static BenchmarkOptions Parse(string[] args)
        {
            string? cli = null;
            string? output = null;
            var samples = 5;
            for (var index = 0; index < args.Length; index += 2)
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("Options require values: --cli <assembly> --output <json> [--samples <3-20>].");
                }

                switch (args[index])
                {
                    case "--cli":
                        cli = args[index + 1];
                        break;
                    case "--output":
                        output = args[index + 1];
                        break;
                    case "--samples" when int.TryParse(
                        args[index + 1],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var parsed):
                        samples = parsed;
                        break;
                    default:
                        throw new ArgumentException($"Unknown or invalid option '{args[index]}'.");
                }
            }

            if (string.IsNullOrWhiteSpace(cli) || string.IsNullOrWhiteSpace(output) || samples is < 3 or > 20)
            {
                throw new ArgumentException("Use --cli <assembly> --output <json> [--samples <3-20>].");
            }

            return new BenchmarkOptions(cli, output, samples);
        }
    }

    private sealed record ProcessResult(
        int ExitCode,
        string Output,
        string Error,
        TimeSpan Elapsed,
        long PeakWorkingSetBytes);
}

internal static class SyntheticAdrGenerator
{
    internal static void Generate(string directory, int count, bool includeErrors)
    {
        Directory.CreateDirectory(directory);
        for (var id = 1; id <= count; id++)
        {
            var status = includeErrors && id % 100 == 0
                ? "Approved"
                : id % 17 == 0
                    ? "Proposed"
                    : id % 13 == 0 ? "Deprecated" : "Accepted";
            var relationships = id > 1 && id % 10 == 0
                ? "\n## Dependencies\n[Foundation](0001-synthetic-decision-0001.md)\n"
                : string.Empty;
            var references = id > 1 && id % 7 == 0
                ? $"\n## References\n[Previous decision]({id - 1:D4}-synthetic-decision-{id - 1:D4}.md)\n"
                : string.Empty;
            var content = $"""
                # Synthetic decision {id:D4}

                ## Status
                {status}

                ## Context
                This deterministic synthetic ADR models bounded architecture documentation for performance measurement.
                It contains no production names, repository content, credentials, or externally sourced data.

                ## Decision
                Adopt synthetic option {id % 11:D2} for scenario partition {id % 5:D2} and retain deterministic ordering.

                ## Consequences
                The generated decision provides stable parsing work, repeatable indexing input, and explicit trade-offs.
                Operators must treat these measurements as environment-specific evidence rather than a service-level objective.
                {relationships}{references}
                """.ReplaceLineEndings("\n");
            File.WriteAllText(
                Path.Combine(directory, $"{id:D4}-synthetic-decision-{id:D4}.md"),
                content,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }

    internal static string ComputeHash(string directory)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var path in Directory.EnumerateFiles(directory, "*.md").Order(StringComparer.Ordinal))
        {
            var name = Encoding.UTF8.GetBytes(Path.GetFileName(path));
            hash.AppendData(name);
            hash.AppendData([0]);
            hash.AppendData(File.ReadAllBytes(path));
            hash.AppendData([0]);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}

internal sealed record BenchmarkReport(
    string SchemaVersion,
    DateTimeOffset RecordedAtUtc,
    string SourceRevision,
    bool? WorkingTreeDirty,
    string CliVersion,
    int SamplesPerScenario,
    EnvironmentMetadata Environment,
    IReadOnlyList<ScenarioResult> Results);

internal sealed record EnvironmentMetadata(
    string OperatingSystem,
    string OsArchitecture,
    string ProcessArchitecture,
    string DotNetRuntime,
    int LogicalProcessorCount,
    long AvailableMemoryBytes);

internal sealed record ScenarioResult(
    int AdrCount,
    string Operation,
    string DatasetSha256,
    double PreparationMilliseconds,
    SampleResult FirstRun,
    SampleSummary SubsequentRuns);

internal sealed record SampleResult(
    double ElapsedMilliseconds,
    long PeakWorkingSetBytes,
    int ExitCode);

internal sealed record SampleSummary(
    int Count,
    double MedianElapsedMilliseconds,
    double MinimumElapsedMilliseconds,
    double MaximumElapsedMilliseconds,
    double SpreadMilliseconds,
    long MedianPeakWorkingSetBytes);
