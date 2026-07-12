namespace TiHiY.StreamControlCenter.UI;

internal static class Theme
{
    public static readonly Color Window = Color.FromArgb(15, 18, 24);
    public static readonly Color Panel = Color.FromArgb(24, 29, 38);
    public static readonly Color PanelAlt = Color.FromArgb(31, 37, 47);
    public static readonly Color Accent = Color.FromArgb(75, 220, 135);
    public static readonly Color AccentMuted = Color.FromArgb(35, 95, 66);
    public static readonly Color Text = Color.FromArgb(238, 244, 247);
    public static readonly Color MutedText = Color.FromArgb(160, 172, 182);
    public static readonly Color Danger = Color.FromArgb(235, 91, 91);

    public static Button CreateButton(string text, bool danger = false)
    {
        return new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(128, 38),
            FlatStyle = FlatStyle.Flat,
            BackColor = danger ? Danger : AccentMuted,
            ForeColor = Text,
            Font = new Font("Segoe UI Semibold", 9.5f),
            Margin = new Padding(6),
            Padding = new Padding(10, 4, 10, 4),
            Cursor = Cursors.Hand
        };
    }

    public static Label CreateLabel(string text, bool muted = false)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = muted ? MutedText : Text,
            Font = new Font("Segoe UI", 9.5f),
            Margin = new Padding(6)
        };
    }
}
