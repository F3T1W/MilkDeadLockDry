using DeadLocky.Entities.Game.Model;
using DeadLocky.Entities.Steam.Model;

namespace DeadLocky.Features.PrepareGame.Model;

internal enum PreparationAction
{
    None,
    Checking,
    SigningIn,
    ChoosingFolder,
    Downloading
}

internal sealed record GamePreparationState(
    GameDirectory Directory,
    SteamAccount? Account = null,
    bool IsDownloaded = false,
    PreparationAction Action = PreparationAction.None,
    GameDownloadProgress? Progress = null,
    string? Error = null)
{
    public bool IsBusy => Action != PreparationAction.None;
}
