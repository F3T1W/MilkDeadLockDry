using DeadLocky.Features.SelectGameDirectory.Ui;
using DeadLocky.Shared.Api.Directories;

namespace DeadLocky.Pages.Launcher.Ui;

public sealed class LauncherWindowController : NSWindowController
{
    private readonly SelectGameDirectoryView _selection;
    private bool _disposed;

    public LauncherWindowController(Func<NSWindow, IDirectoryPicker> createPicker) : base(CreateWindow())
    {
        ArgumentNullException.ThrowIfNull(createPicker);
        _selection = new SelectGameDirectoryView(createPicker(Window));
        var content = new NSView(new CGRect(0, 0, 640, 300));
        var stack = new NSStackView
        {
            Orientation = NSUserInterfaceLayoutOrientation.Vertical,
            Alignment = NSLayoutAttribute.Leading,
            Distribution = NSStackViewDistribution.Fill,
            Spacing = 16,
            TranslatesAutoresizingMaskIntoConstraints = false
        };
        var description = NSTextField.CreateLabel("Select your Deadlock installation folder.");
        description.TextColor = NSColor.SecondaryLabel;
        stack.AddArrangedSubview(description);
        stack.AddArrangedSubview(_selection);
        content.AddSubview(stack);
        Window.ContentView = content;
        NSLayoutConstraint.ActivateConstraints(
        [
            stack.LeadingAnchor.ConstraintEqualTo(content.LeadingAnchor, 24),
            stack.TrailingAnchor.ConstraintEqualTo(content.TrailingAnchor, -24),
            stack.TopAnchor.ConstraintEqualTo(content.TopAnchor, 24),
            _selection.WidthAnchor.ConstraintEqualTo(stack.WidthAnchor)
        ]);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _selection.Dispose();
        }

        base.Dispose(disposing);
    }

    private static NSWindow CreateWindow()
    {
        const NSWindowStyle style = NSWindowStyle.Titled | NSWindowStyle.Closable
            | NSWindowStyle.Miniaturizable | NSWindowStyle.Resizable;
        var window = new NSWindow(new CGRect(0, 0, 640, 300), style, NSBackingStore.Buffered, false)
        {
            Title = "DeadLocky",
            ContentMinSize = new CGSize(520, 260)
        };
        window.Center();
        return window;
    }
}
