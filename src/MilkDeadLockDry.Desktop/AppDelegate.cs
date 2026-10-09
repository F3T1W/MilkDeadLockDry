using MilkDeadLockDry.Desktop.ViewModels;
using MilkDeadLockDry.Desktop.Views;
using ObjCRuntime;

namespace MilkDeadLockDry.Desktop;

[Register("AppDelegate")]
internal sealed class AppDelegate : NSApplicationDelegate
{
    private MainWindowController? _mainWindowController;

    public override void DidFinishLaunching(NSNotification notification)
    {
        NSApplication.SharedApplication.ActivationPolicy = NSApplicationActivationPolicy.Regular;
        ConfigureMenu();
        _mainWindowController = new MainWindowController(new MainViewModel());
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

    private static void ConfigureMenu()
    {
        var menu = new NSMenu();
        var applicationMenu = new NSMenu("MilkDeadLockDry");
        applicationMenu.AddItem(new NSMenuItem("Quit MilkDeadLockDry", new Selector("terminate:"), "q"));
        menu.AddItem(new NSMenuItem { Submenu = applicationMenu });

        var editMenu = new NSMenu("Edit");
        editMenu.AddItem(new NSMenuItem("Copy", new Selector("copy:"), "c"));
        editMenu.AddItem(new NSMenuItem("Select All", new Selector("selectAll:"), "a"));
        menu.AddItem(new NSMenuItem { Title = "Edit", Submenu = editMenu });
        NSApplication.SharedApplication.MainMenu = menu;
    }
}
