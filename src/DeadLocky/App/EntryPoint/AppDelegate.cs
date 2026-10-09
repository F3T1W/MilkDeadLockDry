using DeadLocky.App.Composition;
using DeadLocky.App.Ui;
using DeadLocky.Pages.Launcher.Ui;

namespace DeadLocky.App.EntryPoint;

[Register("AppDelegate")]
internal sealed class AppDelegate : NSApplicationDelegate
{
    private LauncherWindowController? _mainWindowController;

    public override void DidFinishLaunching(NSNotification notification)
    {
        NSApplication.SharedApplication.ActivationPolicy = NSApplicationActivationPolicy.Regular;
        ApplicationMenu.Install();
        _mainWindowController = LauncherComposition.CreateLauncher();
        _mainWindowController.ShowWindow(this);
        NSApplication.SharedApplication.Activate();
    }

    public override bool ApplicationShouldTerminateAfterLastWindowClosed(NSApplication sender)
    {
        return true;
    }

    public override void WillTerminate(NSNotification notification)
    {
        _mainWindowController?.Dispose();
        _mainWindowController = null;
    }
}
