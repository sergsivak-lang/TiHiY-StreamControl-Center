using System.Globalization;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TiHiY.StreamControlCenter.Services;

public static class ThemePreviewRenderer
{
    public static ImageSource Render(ThemeService.ThemeInfo theme, int width = 640, int height = 360)
    {
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            DrawBackground(drawing, theme, width, height);
            DrawHeader(drawing, theme, width);

            var gap = 8d;
            var contentTop = 94d;
            var footerHeight = 62d;
            var contentBottom = height - footerHeight - 12d;
            var panelWidth = (width - gap * 3) / 2d;
            var panelHeight = (contentBottom - contentTop - gap) / 2d;

            DrawChatPanel(drawing, theme, new Rect(gap, contentTop, panelWidth, panelHeight));
            DrawDonationsPanel(drawing, theme, new Rect(gap * 2 + panelWidth, contentTop, panelWidth, panelHeight));
            DrawMixerPanel(drawing, theme, new Rect(gap, contentTop + panelHeight + gap, panelWidth, panelHeight));
            DrawNotificationsPanel(drawing, theme, new Rect(gap * 2 + panelWidth, contentTop + panelHeight + gap, panelWidth, panelHeight));

            var footerTop = height - footerHeight - 6d;
            var footerWidth = (width - gap * 4) / 3d;
            DrawFooterPanel(drawing, theme, new Rect(gap, footerTop, footerWidth, footerHeight), "SYSTEM", 0);
            DrawFooterPanel(drawing, theme, new Rect(gap * 2 + footerWidth, footerTop, footerWidth, footerHeight), "UKRAINE", 1);
            DrawFooterPanel(drawing, theme, new Rect(gap * 3 + footerWidth * 2, footerTop, footerWidth, footerHeight), "AIDA64 LIVE", 2);
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static void DrawBackground(DrawingContext drawing, ThemeService.ThemeInfo theme, int width, int height)
    {
        var background = new LinearGradientBrush(theme.Palette.Bg0, theme.Palette.Bg1, 90);
        drawing.DrawRectangle(background, null, new Rect(0, 0, width, height));
        drawing.DrawRoundedRectangle(null, new Pen(new SolidColorBrush(theme.Palette.Cyan), 2), new Rect(2, 2, width - 4, height - 4), 10, 10);
        drawing.DrawRoundedRectangle(null, new Pen(new SolidColorBrush(theme.Palette.Amber), 1), new Rect(6, 6, width - 12, height - 12), 8, 8);

        var glow = new RadialGradientBrush(Color.FromArgb(90, theme.Palette.Cyan.R, theme.Palette.Cyan.G, theme.Palette.Cyan.B), Colors.Transparent)
        {
            Center = new Point(0.52, 0.15),
            GradientOrigin = new Point(0.52, 0.15),
            RadiusX = 0.45,
            RadiusY = 0.5
        };
        drawing.DrawRectangle(glow, null, new Rect(0, 0, width, height));
    }

    private static void DrawHeader(DrawingContext drawing, ThemeService.ThemeInfo theme, int width)
    {
        var gold = new SolidColorBrush(theme.Palette.Amber);
        var cyan = new SolidColorBrush(theme.Palette.Cyan);
        var text = new SolidColorBrush(theme.Palette.Text);
        DrawShield(drawing, theme, new Rect(18, 13, 58, 64));
        DrawText(drawing, "TiHiY", 28, FontWeights.Black, text, new Point(88, 13));
        DrawText(drawing, "StreamControl Center", 24, FontWeights.Bold, gold, new Point(162, 17));
        DrawText(drawing, theme.Name.ToUpperInvariant(), 11, FontWeights.SemiBold, cyan, new Point(90, 51));

        var pillTop = 66d;
        var pillWidth = 95d;
        for (var i = 0; i < 4; i++)
        {
            var rect = new Rect(88 + i * (pillWidth + 5), pillTop, pillWidth, 24);
            drawing.DrawRoundedRectangle(new SolidColorBrush(theme.Palette.Panel2), new Pen(new SolidColorBrush(i == 3 ? theme.Palette.Red : theme.Palette.Line), 1), rect, 4, 4);
            drawing.DrawEllipse(new SolidColorBrush(i == 1 ? theme.Palette.Yellow : theme.Palette.Green), null, new Point(rect.X + 11, rect.Y + 12), 4, 4);
            DrawText(drawing, i switch { 0 => "OBS", 1 => "MULTI", 2 => "TWITCH", _ => "YOUTUBE" }, 8.5, FontWeights.Bold, text, new Point(rect.X + 20, rect.Y + 6));
        }

        var actionWidth = 100d;
        for (var i = 0; i < 3; i++)
        {
            var rect = new Rect(width - 318 + i * (actionWidth + 5), 22, actionWidth, 34);
            drawing.DrawRoundedRectangle(new SolidColorBrush(theme.Palette.ButtonMid), new Pen(new SolidColorBrush(i == 1 ? theme.Palette.Red : theme.Palette.Amber), 1.2), rect, 5, 5);
            DrawText(drawing, i switch { 0 => "SETTINGS", 1 => "BROADCAST", _ => "MUSIC" }, 8.5, FontWeights.Bold, text, new Point(rect.X + 10, rect.Y + 11));
        }
    }

    private static void DrawPanelFrame(DrawingContext drawing, ThemeService.ThemeInfo theme, Rect rect, string title)
    {
        var panelBrush = new LinearGradientBrush(theme.Palette.PanelTop, theme.Palette.PanelBottom, 90);
        drawing.DrawRoundedRectangle(panelBrush, new Pen(new SolidColorBrush(theme.Palette.Amber), 1.15), rect, 6, 6);
        drawing.DrawLine(new Pen(new SolidColorBrush(theme.Palette.Line), 0.8), new Point(rect.X + 9, rect.Y + 27), new Point(rect.Right - 9, rect.Y + 27));
        DrawText(drawing, title, 11, FontWeights.Bold, new SolidColorBrush(theme.Palette.Amber), new Point(rect.X + 12, rect.Y + 7));
        DrawCornerOrnament(drawing, theme, rect);
    }

    private static void DrawChatPanel(DrawingContext drawing, ThemeService.ThemeInfo theme, Rect rect)
    {
        DrawPanelFrame(drawing, theme, rect, "MULTICHAT • TWITCH + YOUTUBE");
        var colors = new[] { theme.Palette.Purple, theme.Palette.Red, theme.Palette.Cyan, theme.Palette.Green };
        for (var i = 0; i < 5; i++)
        {
            var y = rect.Y + 38 + i * 16;
            DrawText(drawing, $"20:3{i}:2{i}", 7.5, FontWeights.Normal, new SolidColorBrush(theme.Palette.Muted), new Point(rect.X + 12, y));
            drawing.DrawRoundedRectangle(new SolidColorBrush(colors[i % colors.Length]), null, new Rect(rect.X + 60, y + 1, 9, 9), 2, 2);
            DrawText(drawing, i % 2 == 0 ? "User123" : "Nightbot", 8, FontWeights.Bold, new SolidColorBrush(colors[i % colors.Length]), new Point(rect.X + 74, y));
            drawing.DrawLine(new Pen(new SolidColorBrush(theme.Palette.Text), 1), new Point(rect.X + 132, y + 6), new Point(rect.Right - 18 - i * 5, y + 6));
        }
        drawing.DrawRoundedRectangle(new SolidColorBrush(theme.Palette.Bg0), new Pen(new SolidColorBrush(theme.Palette.Line), 0.8), new Rect(rect.X + 10, rect.Bottom - 25, rect.Width - 20, 17), 3, 3);
    }

    private static void DrawDonationsPanel(DrawingContext drawing, ThemeService.ThemeInfo theme, Rect rect)
    {
        DrawPanelFrame(drawing, theme, rect, "DONATIONS");
        DrawText(drawing, "250 UAH", 22, FontWeights.Black, new SolidColorBrush(theme.Palette.Amber), new Point(rect.X + 25, rect.Y + 42));
        DrawText(drawing, "Vitalik", 10, FontWeights.Bold, new SolidColorBrush(theme.Palette.Text), new Point(rect.X + 27, rect.Y + 72));
        for (var i = 0; i < 2; i++)
        {
            var item = new Rect(rect.X + rect.Width * 0.52, rect.Y + 38 + i * 38, rect.Width * 0.43, 31);
            drawing.DrawRoundedRectangle(new SolidColorBrush(theme.Palette.Panel2), new Pen(new SolidColorBrush(theme.Palette.Line), 0.8), item, 4, 4);
            drawing.DrawEllipse(new SolidColorBrush(i == 0 ? theme.Palette.Amber : theme.Palette.Red), null, new Point(item.X + 12, item.Y + 15), 7, 7);
            drawing.DrawLine(new Pen(new SolidColorBrush(theme.Palette.Text), 1.2), new Point(item.X + 24, item.Y + 11), new Point(item.Right - 18, item.Y + 11));
            drawing.DrawLine(new Pen(new SolidColorBrush(theme.Palette.Muted), 1), new Point(item.X + 24, item.Y + 21), new Point(item.Right - 36, item.Y + 21));
        }
    }

    private static void DrawMixerPanel(DrawingContext drawing, ThemeService.ThemeInfo theme, Rect rect)
    {
        DrawPanelFrame(drawing, theme, rect, "QUICK MIXER • OBS AUDIO");
        for (var i = 0; i < 4; i++)
        {
            var y = rect.Y + 38 + i * 19;
            drawing.DrawRoundedRectangle(new SolidColorBrush(theme.Palette.Panel2), new Pen(new SolidColorBrush(theme.Palette.LineSoft), 0.8), new Rect(rect.X + 10, y, rect.Width - 20, 15), 3, 3);
            drawing.DrawEllipse(new SolidColorBrush(theme.Palette.Amber), null, new Point(rect.X + 21, y + 7.5), 4, 4);
            var bar = new Rect(rect.X + 44, y + 5, rect.Width - 108, 5);
            drawing.DrawRoundedRectangle(new SolidColorBrush(theme.Palette.Bg0), null, bar, 2, 2);
            var level = new LinearGradientBrush(theme.Palette.Green, theme.Palette.Amber, 0);
            drawing.DrawRoundedRectangle(level, null, new Rect(bar.X, bar.Y, bar.Width * (0.55 + i * 0.08), bar.Height), 2, 2);
            drawing.DrawEllipse(new SolidColorBrush(theme.Palette.Text), null, new Point(bar.X + bar.Width * (0.55 + i * 0.08), bar.Y + 2.5), 4, 4);
        }
    }

    private static void DrawNotificationsPanel(DrawingContext drawing, ThemeService.ThemeInfo theme, Rect rect)
    {
        DrawPanelFrame(drawing, theme, rect, "NOTIFICATIONS");
        for (var i = 0; i < 4; i++)
        {
            var y = rect.Y + 38 + i * 19;
            drawing.DrawEllipse(new SolidColorBrush(i < 2 ? theme.Palette.Red : theme.Palette.Amber), null, new Point(rect.X + 19, y + 6), 6, 6);
            drawing.DrawLine(new Pen(new SolidColorBrush(theme.Palette.Text), 1.2), new Point(rect.X + 32, y + 3), new Point(rect.X + 105, y + 3));
            drawing.DrawLine(new Pen(new SolidColorBrush(theme.Palette.Muted), 1), new Point(rect.X + 32, y + 10), new Point(rect.Right - 25, y + 10));
        }
    }

    private static void DrawFooterPanel(DrawingContext drawing, ThemeService.ThemeInfo theme, Rect rect, string title, int kind)
    {
        DrawPanelFrame(drawing, theme, rect, title);
        if (kind == 0)
        {
            for (var i = 0; i < 3; i++)
            {
                drawing.DrawEllipse(new SolidColorBrush(theme.Palette.Green), null, new Point(rect.X + 16, rect.Y + 36 + i * 8), 2.5, 2.5);
                drawing.DrawLine(new Pen(new SolidColorBrush(theme.Palette.Muted), 0.8), new Point(rect.X + 23, rect.Y + 36 + i * 8), new Point(rect.Right - 15, rect.Y + 36 + i * 8));
            }
        }
        else if (kind == 1)
        {
            DrawShield(drawing, theme, new Rect(rect.X + rect.Width / 2 - 18, rect.Y + 29, 36, 38));
        }
        else
        {
            var labels = new[] { "CPU", "GPU", "RAM", "FPS" };
            var values = new[] { "52°", "54°", "31%", "60" };
            var gap = 3d;
            var x = rect.X + 8;
            var width = (rect.Width - 16 - gap * 3) / 4d;
            for (var i = 0; i < 4; i++)
            {
                var card = new Rect(x + i * (width + gap), rect.Y + 31, width, 24);
                drawing.DrawRoundedRectangle(
                    new SolidColorBrush(theme.Palette.Panel2),
                    new Pen(new SolidColorBrush(i == 2 ? theme.Palette.Amber : theme.Palette.Cyan), 0.9),
                    card,
                    3,
                    3);
                DrawText(drawing, labels[i], 5.3, FontWeights.Bold, new SolidColorBrush(theme.Palette.Amber), new Point(card.X + 4, card.Y + 3));
                DrawText(drawing, values[i], 8.5, FontWeights.Black, new SolidColorBrush(theme.Palette.Text), new Point(card.X + 4, card.Y + 11));
            }
        }
    }

    private static void DrawShield(DrawingContext drawing, ThemeService.ThemeInfo theme, Rect rect)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(rect.X + rect.Width * 0.5, rect.Y), true, true);
            context.LineTo(new Point(rect.Right, rect.Y + rect.Height * 0.18), true, false);
            context.LineTo(new Point(rect.Right, rect.Y + rect.Height * 0.68), true, false);
            context.LineTo(new Point(rect.X + rect.Width * 0.5, rect.Bottom), true, false);
            context.LineTo(new Point(rect.X, rect.Y + rect.Height * 0.68), true, false);
            context.LineTo(new Point(rect.X, rect.Y + rect.Height * 0.18), true, false);
        }
        geometry.Freeze();
        drawing.DrawGeometry(new SolidColorBrush(theme.Palette.ButtonMid), new Pen(new SolidColorBrush(theme.Palette.Amber), Math.Max(1, rect.Width * 0.035)), geometry);

        var pen = new Pen(new SolidColorBrush(theme.Palette.Amber), Math.Max(1, rect.Width * 0.045)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        var cx = rect.X + rect.Width * 0.5;
        drawing.DrawLine(pen, new Point(cx, rect.Y + rect.Height * 0.24), new Point(cx, rect.Y + rect.Height * 0.72));
        drawing.DrawLine(pen, new Point(rect.X + rect.Width * 0.28, rect.Y + rect.Height * 0.30), new Point(rect.X + rect.Width * 0.28, rect.Y + rect.Height * 0.53));
        drawing.DrawLine(pen, new Point(rect.X + rect.Width * 0.72, rect.Y + rect.Height * 0.30), new Point(rect.X + rect.Width * 0.72, rect.Y + rect.Height * 0.53));
        drawing.DrawLine(pen, new Point(rect.X + rect.Width * 0.28, rect.Y + rect.Height * 0.53), new Point(cx, rect.Y + rect.Height * 0.78));
        drawing.DrawLine(pen, new Point(rect.X + rect.Width * 0.72, rect.Y + rect.Height * 0.53), new Point(cx, rect.Y + rect.Height * 0.78));
        drawing.DrawEllipse(null, pen, new Point(cx, rect.Y + rect.Height * 0.62), rect.Width * 0.13, rect.Height * 0.10);
    }

    private static void DrawCornerOrnament(DrawingContext drawing, ThemeService.ThemeInfo theme, Rect rect)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(150, theme.Palette.Amber.R, theme.Palette.Amber.G, theme.Palette.Amber.B)), 0.8);
        for (var i = 0; i < 4; i++)
        {
            drawing.DrawLine(pen, new Point(rect.X + 5 + i * 3, rect.Y + 5), new Point(rect.X + 5, rect.Y + 5 + i * 5));
            drawing.DrawLine(pen, new Point(rect.Right - 5 - i * 3, rect.Bottom - 5), new Point(rect.Right - 5, rect.Bottom - 5 - i * 5));
        }
    }

    private static void DrawText(DrawingContext drawing, string text, double size, FontWeight weight, Brush brush, Point origin)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal),
            size,
            brush,
            1.0);
        drawing.DrawText(formatted, origin);
    }
}
