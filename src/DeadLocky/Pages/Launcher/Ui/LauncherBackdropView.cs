using DeadLocky.Shared.Ui;

namespace DeadLocky.Pages.Launcher.Ui;

internal sealed class LauncherBackdropView : NSView
{
    public LauncherBackdropView()
    {
        AutoresizingMask = NSViewResizingMask.WidthSizable | NSViewResizingMask.HeightSizable;
        WantsLayer = true;
        Layer!.NeedsDisplayOnBoundsChange = true;
    }

    public override void DrawRect(CGRect dirtyRect)
    {
        using var background = new NSGradient(
            LauncherStyle.Background,
            NSColor.FromRgb(0.13f, 0.13f, 0.135f));
        background.DrawInRect(Bounds, 35);
    }
}
