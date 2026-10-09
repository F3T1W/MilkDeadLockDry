using ObjCRuntime;

namespace DeadLocky.App.Ui;

internal static class ApplicationMenu
{
    public static void Install()
    {
        var menu = new NSMenu();
        var applicationMenu = new NSMenu("DeadLocky");
        applicationMenu.AddItem(new NSMenuItem("Quit DeadLocky", new Selector("terminate:"), "q"));
        menu.AddItem(new NSMenuItem { Submenu = applicationMenu });

        var editMenu = new NSMenu("Edit");
        editMenu.AddItem(new NSMenuItem("Copy", new Selector("copy:"), "c"));
        editMenu.AddItem(new NSMenuItem("Select All", new Selector("selectAll:"), "a"));
        menu.AddItem(new NSMenuItem { Title = "Edit", Submenu = editMenu });
        NSApplication.SharedApplication.MainMenu = menu;
    }
}
