using AdrGuard.Cli;

using var cancellationSource =
    new CancellationTokenSource();

var supportsGracefulCancellation =
    args.Length > 0
    && string.Equals(
        args[0],
        "draft",
        StringComparison.Ordinal);

ConsoleCancelEventHandler cancelHandler =
    (_, eventArgs) =>
    {
        if (!supportsGracefulCancellation
            || cancellationSource.IsCancellationRequested)
        {
            return;
        }

        eventArgs.Cancel = true;
        cancellationSource.Cancel();
    };

Console.CancelKeyPress += cancelHandler;

try
{
    return CliApplication.Run(
        args,
        Console.Out,
        Console.Error,
        cancellationToken:
            cancellationSource.Token);
}
finally
{
    Console.CancelKeyPress -= cancelHandler;
}
