using AdrGuard.Agent;

using var cancellationSource = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};

return await AgentApplication.RunAsync(
    args,
    Console.Out,
    Console.Error,
    cancellationToken: cancellationSource.Token,
    input: Console.In,
    isInteractive: !Console.IsInputRedirected).ConfigureAwait(false);
