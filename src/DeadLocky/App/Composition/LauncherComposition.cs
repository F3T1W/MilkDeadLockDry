using DeadLocky.Pages.Launcher.Ui;
using DeadLocky.Shared.Api.Directories;

namespace DeadLocky.App.Composition;

internal static class LauncherComposition
{
    public static LauncherWindowController CreateLauncher()
    {
        return new LauncherWindowController(window => new AppKitDirectoryPicker(window));
    }
}
