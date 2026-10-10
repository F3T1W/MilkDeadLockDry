using DeadLocky.Entities.Game.Model;
using DeadLocky.Entities.Steam.Model;
using DeadLocky.Features.PrepareGame.Model;

namespace DeadLocky.Features.PrepareGame.Api;

public interface IGamePreparationService : IDisposable
{
    event EventHandler? SessionLost;

    GameDirectory ReadDirectory();

    void SaveDirectory(GameDirectory directory);

    Task<bool> IsDownloadedAsync(GameDirectory directory, CancellationToken cancellationToken);

    Task<SteamAccount?> RestoreSessionAsync(CancellationToken cancellationToken);

    Task<SteamAccount> SignInAsync(CancellationToken cancellationToken);

    Task DownloadAsync(GameDirectory directory, IProgress<GameDownloadProgress> progress,
        CancellationToken cancellationToken);
}
