using AdrGuard.Cli;

using var cancellationSource =
    new CancellationTokenSource();

ConsoleCancelEventHandler cancelHandler =
    (_, eventArgs) =>
    {
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
