namespace TiHiY.StreamControlCenter.Services;

/// <summary>
/// DPI-independent platform logo used in compact counters, chat rows and buttons.
/// </summary>
internal sealed class PlatformVectorIcon : FrameworkElement
{
    private readonly string _platform;

    public PlatformVectorIcon(string? platform)
    {
        _platform = platform?.Trim().ToUpperInvariant() ?? string.Empty;
        SnapsToDevicePixels = true;
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var size = Math.Max(1, Math.Min(ActualWidth, ActualHeight));
        var left = (ActualWidth - size) / 2;
        var top = (ActualHeight - size) / 2;
        Point P(double x, double y) => new(left + x * size, top + y * size);
        var white = Brushes.White;
        var gold = new SolidColorBrush(Color.FromRgb(255, 210, 41));
        var pen = new Pen(white, Math.Max(1.2, size * 0.075))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        switch (_platform)
        {
            case "YOUTUBE":
            {
                var red = new SolidColorBrush(Color.FromRgb(255, 0, 51));
                dc.DrawRoundedRectangle(red, null,
                    new Rect(P(.08, .20), P(.92, .80)), size * .16, size * .16);
                var play = new StreamGeometry();
                using (var ctx = play.Open())
                {
                    ctx.BeginFigure(P(.41, .32), true, true);
                    ctx.LineTo(P(.72, .50), true, false);
                    ctx.LineTo(P(.41, .68), true, false);
                }
                play.Freeze();
                dc.DrawGeometry(white, null, play);
                break;
            }
            case "TWITCH":
            {
                var purple = new SolidColorBrush(Color.FromRgb(145, 71, 255));
                var bubble = new StreamGeometry();
                using (var ctx = bubble.Open())
                {
                    ctx.BeginFigure(P(.12, .12), true, true);
                    ctx.LineTo(P(.88, .12), true, false);
                    ctx.LineTo(P(.88, .66), true, false);
                    ctx.LineTo(P(.65, .88), true, false);
                    ctx.LineTo(P(.48, .88), true, false);
                    ctx.LineTo(P(.48, .75), true, false);
                    ctx.LineTo(P(.12, .75), true, false);
                }
                bubble.Freeze();
                dc.DrawGeometry(purple, null, bubble);
                var innerPen = new Pen(white, Math.Max(1.25, size * .075))
                {
                    StartLineCap = PenLineCap.Square,
                    EndLineCap = PenLineCap.Square
                };
                dc.DrawLine(innerPen, P(.40, .31), P(.40, .57));
                dc.DrawLine(innerPen, P(.64, .31), P(.64, .57));
                break;
            }
            case "DONATELLO":
            {
                var heart = new StreamGeometry();
                using var ctx = heart.Open();
                ctx.BeginFigure(P(.50, .82), true, true);
                ctx.BezierTo(P(.15, .60), P(.16, .28), P(.36, .25), true, false);
                ctx.BezierTo(P(.47, .23), P(.50, .34), P(.50, .34), true, false);
                ctx.BezierTo(P(.50, .34), P(.54, .23), P(.65, .25), true, false);
                ctx.BezierTo(P(.86, .28), P(.85, .60), P(.50, .82), true, false);
                heart.Freeze();
                dc.DrawGeometry(gold, null, heart);
                break;
            }
            case "DISCORD":
            {
                var violet = new SolidColorBrush(Color.FromRgb(88, 101, 242));
                dc.DrawRoundedRectangle(violet, null, new Rect(P(.10, .22), P(.90, .78)), size * .16, size * .16);
                dc.DrawEllipse(white, null, P(.39, .49), size * .055, size * .055);
                dc.DrawEllipse(white, null, P(.61, .49), size * .055, size * .055);
                dc.DrawLine(pen, P(.37, .64), P(.50, .69));
                dc.DrawLine(pen, P(.50, .69), P(.63, .64));
                break;
            }
            default:
                dc.DrawEllipse(null, pen, P(.50, .50), size * .30, size * .30);
                dc.DrawLine(pen, P(.50, .20), P(.50, .80));
                dc.DrawLine(pen, P(.20, .50), P(.80, .50));
                break;
        }
    }
}