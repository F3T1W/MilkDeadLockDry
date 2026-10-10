namespace DeadLocky.Shared.Api.Directories;

public sealed class AppKitDirectoryPicker(NSWindow owner) : IDirectoryPicker
{
    public async Task<string?> PickAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var session = new PickerSession(owner, cancellationToken);
        return await session.RunAsync();
    }

    private sealed class PickerSession(NSWindow owner, CancellationToken token) : IDisposable
    {
        private readonly TaskCompletionSource<string?> _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly NSOpenPanel _panel = NSOpenPanel.OpenPanel;
        private bool _disposed;

        public void Dispose()
        {
            _disposed = true;
            _panel.Dispose();
        }

        public async Task<string?> RunAsync()
        {
            _panel.CanChooseDirectories = true;
            _panel.CanChooseFiles = false;
            _panel.AllowsMultipleSelection = false;
            _panel.CanCreateDirectories = true;
            _panel.Prompt = "Choose";
            _panel.BeginSheet(owner, OnCompleted);
            await using CancellationTokenRegistration registration = token.Register(OnCancelled);
            return await _completion.Task;
        }

        private void OnCompleted(nint response)
        {
            _ = token.IsCancellationRequested
                ? _completion.TrySetCanceled(token)
                : _completion.TrySetResult(response == (nint)NSModalResponse.OK ? _panel.Url.Path : null);
        }

        private void OnCancelled()
        {
            owner.BeginInvokeOnMainThread(() =>
            {
                if (!_disposed && !_completion.Task.IsCompleted)
                {
                    _panel.Cancel(_panel);
                }
            });
        }
    }
}
