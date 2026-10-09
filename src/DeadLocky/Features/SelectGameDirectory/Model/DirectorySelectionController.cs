using DeadLocky.Entities.Game.Model;
using DeadLocky.Shared.Api.Directories;

namespace DeadLocky.Features.SelectGameDirectory.Model;

internal sealed class DirectorySelectionController(IDirectoryPicker picker) : IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private bool _disposed;

    public event EventHandler? StateChanged;

    public DirectorySelectionState State { get; private set; } = new();

    public async Task ChooseAsync()
    {
        if (_disposed || State.IsChoosing)
        {
            return;
        }

        CancellationToken token = _lifetime.Token;
        DirectorySelectionState previous = State with { Error = null };
        Update(previous with { IsChoosing = true, Error = null });
        try
        {
            string? path = await picker.PickAsync(token);
            Update(previous with { Directory = path is null ? previous.Directory : new GameDirectory(path) });
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Closing the owner cancels the operation and suppresses further rendering.
        }
        catch (Exception exception)
        {
            Update(previous with { Error = exception.Message });
        }
    }

    public void Clear()
    {
        if (!_disposed && !State.IsChoosing)
        {
            Update(new DirectorySelectionState());
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StateChanged = null;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private void Update(DirectorySelectionState state)
    {
        if (_disposed || State == state)
        {
            return;
        }

        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
