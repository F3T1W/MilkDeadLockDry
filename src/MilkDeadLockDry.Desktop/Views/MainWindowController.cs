using System.ComponentModel;
using MilkDeadLockDry.Desktop.ViewModels;

namespace MilkDeadLockDry.Desktop.Views;

internal sealed class MainWindowController : NSWindowController
{
    private const NSWindowStyle Style = NSWindowStyle.Titled | NSWindowStyle.Closable
                                                             | NSWindowStyle.Miniaturizable | NSWindowStyle.Resizable;

    private readonly MainViewModel _viewModel;
    private readonly NSTextField _directory;
    private readonly NSTextField _status;
    private readonly NSButton _chooseButton;
    private readonly NSButton _clearButton;
    private NSOpenPanel? _folderPanel;
    private bool _disposed;

    public MainWindowController(MainViewModel viewModel) : base(CreateWindow())
    {
        _viewModel = viewModel;
        _directory = NSTextField.CreateLabel(string.Empty);
        _directory.Selectable = true;
        _status = NSTextField.CreateLabel(string.Empty);
        _status.TextColor = NSColor.SecondaryLabel;
        _chooseButton = new NSButton
        {
            Title = "Choose game folder…",
            BezelStyle = NSBezelStyle.Rounded
        };
        _clearButton = new NSButton
        {
            Title = "Clear selection",
            BezelStyle = NSBezelStyle.Rounded
        };
        _chooseButton.Activated += OnChooseFolder;
        _clearButton.Activated += OnClearSelection;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ConfigureLayout();
        RefreshState();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _chooseButton.Activated -= OnChooseFolder;
            _clearButton.Activated -= OnClearSelection;
            _folderPanel?.Dispose();
            _folderPanel = null;
        }

        base.Dispose(disposing);
    }

    private static NSWindow CreateWindow()
    {
        var window = new NSWindow(new CGRect(0, 0, 640, 300), Style, NSBackingStore.Buffered, false)
        {
            Title = "MilkDeadLockDry",
            ContentMinSize = new CGSize(520, 260)
        };
        window.Center();
        return window;
    }

    private void ConfigureLayout()
    {
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
        var actions = NSStackView.FromViews([_chooseButton, _clearButton]);
        actions.Orientation = NSUserInterfaceLayoutOrientation.Horizontal;
        actions.Spacing = 12;

        foreach (NSView view in new NSView[] { description, actions, _directory, _status })
        {
            stack.AddArrangedSubview(view);
        }

        content.AddSubview(stack);
        Window.ContentView = content;
        NSLayoutConstraint.ActivateConstraints(
        [
            stack.LeadingAnchor.ConstraintEqualTo(content.LeadingAnchor, 24),
            stack.TrailingAnchor.ConstraintEqualTo(content.TrailingAnchor, -24),
            stack.TopAnchor.ConstraintEqualTo(content.TopAnchor, 24),
            _directory.WidthAnchor.ConstraintEqualTo(stack.WidthAnchor)
        ]);
    }

    private void OnChooseFolder(object? sender, EventArgs args)
    {
        if (_folderPanel is not null)
        {
            return;
        }

        NSOpenPanel panel = NSOpenPanel.OpenPanel;
        _folderPanel = panel;
        panel.CanChooseDirectories = true;
        panel.CanChooseFiles = false;
        panel.AllowsMultipleSelection = false;
        panel.CanCreateDirectories = false;
        panel.Prompt = "Choose";
        panel.BeginSheet(Window, response =>
        {
            if (!_disposed && response == (nint)NSModalResponse.OK && panel.Url.Path is { } path)
            {
                _viewModel.GameDirectory = path;
            }

            panel.Dispose();
            _folderPanel = null;
        });
    }

    private void OnClearSelection(object? sender, EventArgs args)
    {
        _viewModel.ClearSelectionCommand.Execute(null);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        BeginInvokeOnMainThread(RefreshState);
    }

    private void RefreshState()
    {
        if (_disposed)
        {
            return;
        }

        _directory.StringValue = _viewModel.GameDirectory ?? "Choose a folder to continue.";
        _status.StringValue = _viewModel.StatusMessage;
        _clearButton.Enabled = _viewModel.ClearSelectionCommand.CanExecute(null);
    }
}
