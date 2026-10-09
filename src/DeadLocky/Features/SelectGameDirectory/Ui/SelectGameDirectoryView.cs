using DeadLocky.Entities.Game.Model;
using DeadLocky.Features.SelectGameDirectory.Model;
using DeadLocky.Shared.Api.Directories;

namespace DeadLocky.Features.SelectGameDirectory.Ui;

public sealed class SelectGameDirectoryView : NSStackView
{
    private readonly DirectorySelectionController _controller;
    private readonly NSTextField _directory = NSTextField.CreateLabel(string.Empty);
    private readonly NSTextField _status = NSTextField.CreateLabel(string.Empty);
    private readonly NSButton _chooseButton = new()
    {
        Title = "Choose game folder…",
        BezelStyle = NSBezelStyle.Rounded
    };
    private readonly NSButton _clearButton = new() { Title = "Clear selection", BezelStyle = NSBezelStyle.Rounded };
    private GameDirectory? _lastSelection;
    private bool _disposed;

    public SelectGameDirectoryView(IDirectoryPicker picker)
    {
        ArgumentNullException.ThrowIfNull(picker);
        _controller = new DirectorySelectionController(picker);
        _directory.Selectable = true;
        _status.TextColor = NSColor.SecondaryLabel;
        Orientation = NSUserInterfaceLayoutOrientation.Vertical;
        Alignment = NSLayoutAttribute.Leading;
        Distribution = NSStackViewDistribution.Fill;
        Spacing = 16;
        TranslatesAutoresizingMaskIntoConstraints = false;
        var actions = NSStackView.FromViews([_chooseButton, _clearButton]);
        actions.Orientation = NSUserInterfaceLayoutOrientation.Horizontal;
        actions.Spacing = 12;
        AddArrangedSubview(actions);
        AddArrangedSubview(_directory);
        AddArrangedSubview(_status);
        _directory.WidthAnchor.ConstraintEqualTo(WidthAnchor).Active = true;
        _chooseButton.Activated += OnChoose;
        _clearButton.Activated += OnClear;
        _controller.StateChanged += OnStateChanged;
        Render();
    }

    public GameDirectory? SelectedDirectory => _controller.State.Directory;

    public event EventHandler? SelectionChanged;

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _chooseButton.Activated -= OnChoose;
            _clearButton.Activated -= OnClear;
            _controller.StateChanged -= OnStateChanged;
            SelectionChanged = null;
            _controller.Dispose();
        }

        base.Dispose(disposing);
    }

    private async void OnChoose(object? sender, EventArgs args)
    {
        await _controller.ChooseAsync();
    }

    private void OnClear(object? sender, EventArgs args)
    {
        _controller.Clear();
    }

    private void OnStateChanged(object? sender, EventArgs args)
    {
        BeginInvokeOnMainThread(Render);
        if (_lastSelection != SelectedDirectory)
        {
            _lastSelection = SelectedDirectory;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Render()
    {
        if (_disposed)
        {
            return;
        }

        DirectorySelectionState state = _controller.State;
        _directory.StringValue = state.Directory?.Path ?? "Choose a folder to continue.";
        _status.StringValue = state.Error ?? (state.IsChoosing ? "Choosing folder…"
            : state.Directory is null ? "No folder selected." : "Folder selected.");
        _chooseButton.Enabled = !state.IsChoosing;
        _clearButton.Enabled = !state.IsChoosing && state.Directory is not null;
    }
}
