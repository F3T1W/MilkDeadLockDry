namespace DeadLocky.Shared.Api.Directories;

public sealed class AppKitDirectoryPicker(NSWindow owner) : IDirectoryPicker
{
    public async Task<string?> PickAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using NSOpenPanel panel = NSOpenPanel.OpenPanel;
        panel.CanChooseDirectories = true;
        panel.CanChooseFiles = false;
        panel.AllowsMultipleSelection = false;
        panel.CanCreateDirectories = false;
        panel.Prompt = "Choose";
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        panel.BeginSheet(owner, response =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(cancellationToken);
            }
            else
            {
                completion.TrySetResult(response == (nint)NSModalResponse.OK ? panel.Url.Path : null);
            }
        });
        await using CancellationTokenRegistration registration = cancellationToken.Register(() =>
            owner.BeginInvokeOnMainThread(() =>
            {
                if (!completion.Task.IsCompleted)
                {
                    panel.Cancel(panel);
                }
            }));
        return await completion.Task;
    }
}
