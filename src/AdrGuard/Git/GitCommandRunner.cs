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
}
