using DeadLocky.Features.LaunchGame.Api;
using DeadLocky.Features.PrepareGame.Api;
using DeadLocky.Pages.Launcher.Ui;
using DeadLocky.Shared.Api.Directories;

namespace DeadLocky.App.Composition;

internal static class LauncherComposition
{
    public static LauncherWindowController CreateLauncher()
    {
        string settings = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Application Support", "DeadLocky", "settings.json");
        var launcher = new GptkGameLauncher(Path.Combine(Path.GetDirectoryName(settings)!, "launch.json"));
        var service = new SteamClientPreparationService(settings, launcher.ReadSavedSteamAccount,
            launcher.OpenSteamAsync, launcher.DownloadWithSteamAsync, launcher.IsSteamInstallationReady);
        var window = new LauncherWindowController(static owner => new AppKitDirectoryPicker(owner), service);
        window.ConfigureGameLauncher(launcher);
        window.UseSteamClientPreparation();
        return window;
    }
}
