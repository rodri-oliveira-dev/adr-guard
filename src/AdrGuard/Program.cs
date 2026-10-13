using AdrGuard.Cli;

using var cancellationSource =
    new CancellationTokenSource();

var supportsGracefulCancellation =
    args.Length > 0
    && string.Equals(
        args[0],
        "draft",
        StringComparison.Ordinal)
    || (args.Length > 0
        && (string.Equals(args[0], "review", StringComparison.Ordinal)
            || string.Equals(args[0], "impact", StringComparison.Ordinal)))
    || (args.Length > 0
        && string.Equals(args[0], "new", StringComparison.Ordinal)
        && args.Contains("--form", StringComparer.Ordinal))
    || (args.Length > 0
        && string.Equals(args[0], "edit", StringComparison.Ordinal)
        && args.Contains("--form", StringComparer.Ordinal));

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

var usesFormContract = args.Length > 0
    && args[0] is "new" or "edit"
    && args.Contains("--form", StringComparer.Ordinal);
using var formInput = usesFormContract
    ? new StreamReader(
        Console.OpenStandardInput(),
        new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true))
    : null;

try
{
    return CliApplication.Run(
        args,
        formInput ?? Console.In,
        Console.Out,
        Console.Error,
        cancellationSource.Token);
}
finally
{
    Console.CancelKeyPress -= cancelHandler;
}
