using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.UI;

public sealed class OverlayForm : Form
{
    private const int GwlExStyle = -20;
    private const long WsExLayered = 0x00080000L;
    private const long WsExTransparent = 0x00000020L;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExNoActivate = 0x08000000L;
    private const int UlwAlpha = 0x00000002;
    private const byte AcSrcOver = 0x00;
    private const byte AcSrcAlpha = 0x01;
    private const int SwShownoactivate = 4;
    private const uint SwpNosize = 0x0001;
    private const uint SwpNomove = 0x0002;
    private const uint SwpNozorder = 0x0004;
    private const uint SwpNoactivate = 0x0010;
    private const uint SwpFramechanged = 0x0020;
    private const int WmNclbuttondown = 0x00A1;
    private const int Htcaption = 0x0002;

    private readonly List<ChatMessage> _messages = [];
    private readonly object _sync = new();
    private int _backgroundOpacity = 145;
    private bool _clickThrough;
    private int _viewers;
    private int _likes;

    public OverlayForm()
    {
        Text = "TiHiY Chat Overlay";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        MinimumSize = new Size(320, 300);
        Bounds = new Rectangle(40, 80, 560, 760);
        DoubleBuffered = true;

        MouseDown += (_, e) =>
        {
            if (!_clickThrough && e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WmNclbuttondown, Htcaption, 0);
            }
        };
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= unchecked((int)(WsExLayered | WsExToolWindow | WsExNoActivate));
            if (_clickThrough)
            {
                cp.ExStyle |= unchecked((int)WsExTransparent);
            }

            return cp;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        RenderOverlay();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        RenderOverlay();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        RenderOverlay();
    }

    public void BringToFrontWithoutActivation()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        ShowWindow(Handle, SwShownoactivate);
        RenderOverlay();
    }

    public void SetBackgroundOpacity(int opacity)
    {
        _backgroundOpacity = Math.Clamp(opacity, 0, 235);
        RenderOverlay();
    }

    public void SetClickThrough(bool enabled)
    {
        _clickThrough = enabled;
        if (!IsHandleCreated)
        {
            return;
        }

        var style = GetWindowLongPtr(Handle, GwlExStyle).ToInt64();
        style = enabled ? style | WsExTransparent : style & ~WsExTransparent;
        SetWindowLongPtr(Handle, GwlExStyle, new IntPtr(style));
        SetWindowPos(
            Handle,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            SwpNosize | SwpNomove | SwpNozorder | SwpNoactivate | SwpFramechanged);
    }

    public void SetStatistics(int viewers, int likes)
    {
        _viewers = Math.Max(0, viewers);
        _likes = Math.Max(0, likes);
        RenderOverlay();
    }

    public void AddMessage(ChatMessage message)
    {
        lock (_sync)
        {
            _messages.Add(message);
            if (_messages.Count > 80)
            {
                _messages.RemoveRange(0, _messages.Count - 80);
            }
        }

        RenderOverlay();
    }

    private void RenderOverlay()
    {
        if (!IsHandleCreated || IsDisposed || Width <= 1 || Height <= 1)
        {
            return;
        }

        using var bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var outer = new RectangleF(0, 0, Width - 1, Height - 1);
            using var outerPath = RoundedRectangle(outer, 18f);
            using var backgroundBrush = new SolidBrush(Color.FromArgb(_backgroundOpacity, 9, 14, 21));
            graphics.FillPath(backgroundBrush, outerPath);

            DrawHeader(graphics);
            DrawMessages(graphics);
            DrawFooter(graphics);

            using var borderPen = new Pen(Color.FromArgb(210, 70, 215, 132), 1.5f);
            graphics.DrawPath(borderPen, outerPath);
        }

        UpdateLayered(bitmap);
    }

    private void DrawHeader(Graphics graphics)
    {
        using var headerBrush = new SolidBrush(Color.FromArgb(Math.Min(225, _backgroundOpacity + 35), 14, 24, 31));
        using var headerPath = RoundedRectangle(new RectangleF(0, 0, Width - 1, 58), 18f);
        graphics.FillPath(headerBrush, headerPath);

        using var titleFont = new Font("Segoe UI Semibold", 15f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var titleBrush = new SolidBrush(Color.White);
        using var accentBrush = new SolidBrush(Color.FromArgb(75, 220, 135));

        graphics.FillEllipse(accentBrush, 18, 20, 12, 12);
        graphics.DrawString("TiHiY CHAT", titleFont, titleBrush, 40, 16);
    }

    private void DrawMessages(Graphics graphics)
    {
        List<ChatMessage> snapshot;
        lock (_sync)
        {
            snapshot = [.. _messages];
        }

        using var authorFont = new Font("Segoe UI Semibold", 14f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var textFont = new Font("Segoe UI", 14f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var timeFont = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(Color.White);
        using var mutedBrush = new SolidBrush(Color.FromArgb(210, 183, 193, 201));
        using var twitchBrush = new SolidBrush(Color.FromArgb(190, 145, 90, 255));
        using var youtubeBrush = new SolidBrush(Color.FromArgb(210, 255, 70, 70));
        using var botBrush = new SolidBrush(Color.FromArgb(210, 75, 220, 135));

        const float left = 18f;
        var availableWidth = Math.Max(120f, Width - 36f);
        var bottom = Height - 86f;

        for (var i = snapshot.Count - 1; i >= 0; i--)
        {
            var message = snapshot[i];
            var platformBrush = message.IsBot
                ? botBrush
                : message.Platform.Equals("Twitch", StringComparison.OrdinalIgnoreCase)
                    ? twitchBrush
                    : youtubeBrush;

            var authorText = $"{message.Author}";
            var textSize = graphics.MeasureString(message.Text, textFont, new SizeF(availableWidth, 1000f));
            var blockHeight = Math.Max(48f, textSize.Height + 30f);
            bottom -= blockHeight;

            if (bottom < 66f)
            {
                break;
            }

            using var blockPath = RoundedRectangle(new RectangleF(left, bottom, availableWidth, blockHeight - 6f), 10f);
            using var blockBrush = new SolidBrush(Color.FromArgb(Math.Min(205, _backgroundOpacity + 20), 20, 29, 38));
            graphics.FillPath(blockBrush, blockPath);

            graphics.FillEllipse(platformBrush, left + 10f, bottom + 12f, 9f, 9f);
            graphics.DrawString(authorText, authorFont, textBrush, left + 26f, bottom + 7f);
            graphics.DrawString(message.Timestamp.ToString("HH:mm"), timeFont, mutedBrush, left + availableWidth - 48f, bottom + 8f);

            var messageRect = new RectangleF(left + 12f, bottom + 27f, availableWidth - 24f, blockHeight - 30f);
            graphics.DrawString(message.Text, textFont, textBrush, messageRect);
        }

        if (snapshot.Count == 0)
        {
            using var emptyFont = new Font("Segoe UI", 15f, FontStyle.Regular, GraphicsUnit.Pixel);
            graphics.DrawString("Повідомлення чату з'являться тут", emptyFont, mutedBrush, 24f, 82f);
        }
    }

    private void DrawFooter(Graphics graphics)
    {
        var footerRect = new RectangleF(0, Height - 62, Width - 1, 61);
        using var footerPath = RoundedRectangle(footerRect, 18f);
        using var footerBrush = new SolidBrush(Color.FromArgb(Math.Min(225, _backgroundOpacity + 35), 14, 24, 31));
        using var statsFont = new Font("Segoe UI Semibold", 14f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var statsBrush = new SolidBrush(Color.White);
        using var accentBrush = new SolidBrush(Color.FromArgb(75, 220, 135));
        using var likeBrush = new SolidBrush(Color.FromArgb(255, 108, 120));

        graphics.FillPath(footerBrush, footerPath);
        graphics.FillEllipse(accentBrush, 18, Height - 39, 10, 10);
        graphics.DrawString($"Глядачі: {_viewers:N0}", statsFont, statsBrush, 36, Height - 45);

        var likesText = $"Лайки: {_likes:N0}";
        var likesSize = graphics.MeasureString(likesText, statsFont);
        var likesX = Width - likesSize.Width - 20;
        graphics.FillEllipse(likeBrush, likesX - 18, Height - 39, 10, 10);
        graphics.DrawString(likesText, statsFont, statsBrush, likesX, Height - 45);
    }

    private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var diameter = radius * 2f;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void UpdateLayered(Bitmap bitmap)
    {
        var screenDc = GetDC(IntPtr.Zero);
        var memoryDc = CreateCompatibleDC(screenDc);
        var hBitmap = IntPtr.Zero;
        var oldBitmap = IntPtr.Zero;

        try
        {
            hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
            oldBitmap = SelectObject(memoryDc, hBitmap);

            var topPosition = new Point(Left, Top);
            var size = new Size(bitmap.Width, bitmap.Height);
            var sourcePosition = Point.Empty;
            var blend = new BlendFunction
            {
                BlendOp = AcSrcOver,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = AcSrcAlpha
            };

            UpdateLayeredWindow(
                Handle,
                screenDc,
                ref topPosition,
                ref size,
                memoryDc,
                ref sourcePosition,
                0,
                ref blend,
                UlwAlpha);
        }
        finally
        {
            if (oldBitmap != IntPtr.Zero)
            {
                SelectObject(memoryDc, oldBitmap);
            }

            if (hBitmap != IntPtr.Zero)
            {
                DeleteObject(hBitmap);
            }

            DeleteDC(memoryDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int index)
    {
        return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, index) : new IntPtr(GetWindowLong32(hWnd, index));
    }

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr newLong)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, index, newLong)
            : new IntPtr(SetWindowLong32(hWnd, index, newLong.ToInt32()));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteDC(IntPtr hDc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr SelectObject(IntPtr hDc, IntPtr hObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UpdateLayeredWindow(
        IntPtr hWnd,
        IntPtr hdcDst,
        ref Point pptDst,
        ref Size psize,
        IntPtr hdcSrc,
        ref Point pptSrc,
        int crKey,
        ref BlendFunction pblend,
        int dwFlags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
}
