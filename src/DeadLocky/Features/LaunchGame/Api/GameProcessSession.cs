namespace DeadLocky.Features.LaunchGame.Api;

internal sealed class GameProcessSession : IGameSession
{
    private readonly CancellationTokenSource _monitor = new();
    private int _disposed;

    public GameProcessSession(Func<bool> isRunning, Func<CancellationToken, Task>? onExited = null)
    {
        CancellationToken token = _monitor.Token;
        Completion = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), token);
                if (isRunning())
                {
                    continue;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), token);
                if (!isRunning())
                {
                    break;
                }
            }

            token.ThrowIfCancellationRequested();
            if (onExited is not null)
            {
                await onExited(token);
            }
        }, token);
    }

    public Task Completion { get; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _monitor.Cancel();
        _ = Completion.ContinueWith(_ => _monitor.Dispose(), TaskScheduler.Default);
    }
}
