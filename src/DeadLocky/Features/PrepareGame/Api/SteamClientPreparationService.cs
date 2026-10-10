using DeadLocky.Entities.Game.Model;
using DeadLocky.Entities.Steam.Model;
using DeadLocky.Features.PrepareGame.Model;

namespace DeadLocky.Features.PrepareGame.Api;

public sealed class SteamClientPreparationService(
    string settingsPath,
    Func<SteamAccount?> readAccount,
    Func<CancellationToken, Task> openSteam,
    Func<string, CancellationToken, Task> download,
    Func<string, bool> isInstalled) : IGamePreparationService
{
    private readonly InstallationStore _store = new(settingsPath);

    public event EventHandler? SessionLost
    {
        add { }
        remove { }
    }

    public GameDirectory ReadDirectory()
    {
        return _store.ReadDirectory();
    }

    public void SaveDirectory(GameDirectory directory)
    {
        _store.SaveDirectory(directory);
    }

    public async Task<bool> IsDownloadedAsync(GameDirectory directory, CancellationToken cancellationToken)
    {
        return isInstalled(directory.Path) || await InstallationStore.IsDownloadedAsync(directory, cancellationToken);
    }

    public Task<SteamAccount?> RestoreSessionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(readAccount());
    }

    public async Task<SteamAccount> SignInAsync(CancellationToken cancellationToken)
    {
        await openSteam(cancellationToken);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (readAccount() is { } account)
            {
                return account;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }

    public async Task DownloadAsync(GameDirectory directory, IProgress<GameDownloadProgress> progress,
        CancellationToken cancellationToken)
    {
        progress.Report(new GameDownloadProgress(0, 0, "Continue installation or verification in Steam."));
        await download(directory.Path, cancellationToken);
    }

    public void Dispose()
    {
    }
}
