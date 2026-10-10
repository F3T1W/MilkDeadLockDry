using DeadLocky.Shared.Ui;

namespace DeadLocky.Features.PrepareGame.Ui;

internal sealed class PreparationRow : NSView
{
    private readonly NSImageView _status = LauncherStyle.Symbol("circle", LauncherStyle.SecondaryText);

    public PreparationRow(string title, string button, bool primary = false)
    {
        TranslatesAutoresizingMaskIntoConstraints = false;
        NSTextField heading = LauncherStyle.Label(title, 16, NSFontWeight.Semibold);
        Detail = LauncherStyle.Label(string.Empty, 12, NSFontWeight.Regular, LauncherStyle.SecondaryText);
        Detail.LineBreakMode = NSLineBreakMode.TruncatingMiddle;
        Detail.UsesSingleLineMode = true;
        Detail.SetContentCompressionResistancePriority(250, NSLayoutConstraintOrientation.Horizontal);
        Button = LauncherStyle.Button(button, primary);
        foreach (NSView view in new NSView[] { _status, heading, Detail, Button })
        {
            AddSubview(view);
        }

        NSLayoutConstraint.ActivateConstraints(
        [
            HeightAnchor.ConstraintEqualTo(88),
            _status.LeadingAnchor.ConstraintEqualTo(LeadingAnchor, 24),
            _status.CenterYAnchor.ConstraintEqualTo(CenterYAnchor),
            _status.WidthAnchor.ConstraintEqualTo(26),
            _status.HeightAnchor.ConstraintEqualTo(26),
            heading.LeadingAnchor.ConstraintEqualTo(_status.TrailingAnchor, 16),
            heading.TopAnchor.ConstraintEqualTo(TopAnchor, 23),
            Detail.LeadingAnchor.ConstraintEqualTo(heading.LeadingAnchor),
            Detail.TopAnchor.ConstraintEqualTo(heading.BottomAnchor, 5),
            Detail.TrailingAnchor.ConstraintLessThanOrEqualTo(Button.LeadingAnchor, -16),
            Button.TrailingAnchor.ConstraintEqualTo(TrailingAnchor, -24),
            Button.CenterYAnchor.ConstraintEqualTo(CenterYAnchor),
            Button.WidthAnchor.ConstraintEqualTo(144),
            Button.HeightAnchor.ConstraintEqualTo(38)
        ]);
    }

    public NSTextField Detail { get; }

    public NSButton Button { get; }

    public void SetCompleted(bool completed)
    {
        _status.Image = NSImage.GetSystemSymbol(completed ? "checkmark.circle.fill" : "circle", null);
        _status.ContentTintColor = completed ? LauncherStyle.Accent : LauncherStyle.SecondaryText;
    }
}
