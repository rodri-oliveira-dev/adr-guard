using System.Diagnostics;

namespace AdrGuard.Git;

internal static class GitCommandRunner
{
    internal static string Run(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(arguments);
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new GitOperationException("Unable to start Git.");
            string output;
            string error;
            try
            {
                var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
                var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
                process.WaitForExitAsync(cancellationToken).GetAwaiter().GetResult();
                output = standardOutput.GetAwaiter().GetResult();
                error = standardError.GetAwaiter().GetResult().Trim();
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit();
                }

                throw;
            }

            if (process.ExitCode != 0)
            {
                throw new GitOperationException(
                    error.Length == 0
                        ? $"Git exited with code {process.ExitCode}."
                        : $"Git exited with code {process.ExitCode}: {error}");
            }

            return output;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (GitOperationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or System.ComponentModel.Win32Exception
            or IOException
            or UnauthorizedAccessException)
        {
            throw new GitOperationException($"Unable to execute Git: {exception.Message}");
        }
    }

    internal static string RunBounded(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        int maximumOutputCharacters,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumOutputCharacters);

        cancellationToken.ThrowIfCancellationRequested();
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new GitOperationException("Unable to start Git.");
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);

            var outputTask = ReadBoundedAsync(
                process.StandardOutput,
                maximumOutputCharacters,
                "standard output",
                timeoutSource.Token);
            var errorTask = ReadBoundedAsync(
                process.StandardError,
                maximumOutputCharacters,
                "standard error",
                timeoutSource.Token);
            var waitTask = process.WaitForExitAsync(timeoutSource.Token);

            _ = outputTask.ContinueWith(
                _ => Kill(process),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            _ = errorTask.ContinueWith(
                _ => Kill(process),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            try
            {
                Task.WhenAll(waitTask, outputTask, errorTask).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Kill(process);
                throw new GitOperationException(
                    $"Git command exceeded the {timeout.TotalSeconds:0.###}-second time limit.");
            }
            catch
            {
                Kill(process);
                throw;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var output = outputTask.Result;
            var error = errorTask.Result.Trim();
            if (process.ExitCode != 0)
            {
                throw new GitOperationException(
                    error.Length == 0
                        ? $"Git exited with code {process.ExitCode}."
                        : $"Git exited with code {process.ExitCode}: {error}");
            }

            return output;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (GitOperationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or System.ComponentModel.Win32Exception
            or IOException
            or UnauthorizedAccessException)
        {
            throw new GitOperationException($"Unable to execute Git: {exception.Message}");
        }
    }

    private static async Task<string> ReadBoundedAsync(
        StreamReader reader,
        int maximumCharacters,
        string streamName,
        CancellationToken cancellationToken)
    {
        var buffer = new char[Math.Min(8192, maximumCharacters + 1)];
        var output = new System.Text.StringBuilder(
            Math.Min(maximumCharacters, 65536));

        while (true)
        {
            var read = await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return output.ToString();
            }

            if (output.Length + read > maximumCharacters)
            {
                throw new GitOperationException(
                    $"Git {streamName} exceeded the {maximumCharacters}-character limit.");
            }

            output.Append(buffer, 0, read);
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the state check and termination.
        }
    }
}
