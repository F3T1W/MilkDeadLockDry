using DeadLocky.Shared.Ui;
using ObjCRuntime;

namespace DeadLocky.App.Ui;

internal static class ApplicationMenu
{
    private static NSMenu? _applicationMenu;

    public static void ConfigureEnvironmentSetup(Func<Task> setup)
    {
        _applicationMenu?.InsertItem(new NSMenuItem("Game environment…", "",
            (_, _) => _ = UiAction.RunAsync(NSApplication.SharedApplication.MainWindow, setup)), 0);
    }

#if DEBUG
    public static void ConfigureSetupTest(Func<Task> test)
    {
        _applicationMenu?.InsertItem(new NSMenuItem("Test environment setup…", "",
            (_, _) => _ = UiAction.RunAsync(NSApplication.SharedApplication.MainWindow, test)), 1);
    }
#endif

    public static void Install()
    {
        var menu = new NSMenu();
        var applicationMenu = new NSMenu("DeadLocky");
        _applicationMenu = applicationMenu;
        applicationMenu.AddItem(new NSMenuItem("Quit DeadLocky", new Selector("terminate:"), "q"));
        menu.AddItem(new NSMenuItem { Submenu = applicationMenu });

        var editMenu = new NSMenu("Edit");
        editMenu.AddItem(new NSMenuItem("Copy", new Selector("copy:"), "c"));
        editMenu.AddItem(new NSMenuItem("Select All", new Selector("selectAll:"), "a"));
        menu.AddItem(new NSMenuItem { Title = "Edit", Submenu = editMenu });
        NSApplication.SharedApplication.MainMenu = menu;
    }
}
