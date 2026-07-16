namespace TiHiY.StreamControlCenter.Services;

/// <summary>
/// Small platform logo rendered as WPF vectors. It remains readable inside
/// 18–28 px chat badges and does not depend on bitmap decoding or DPI scaling.
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
        var pen = new Pen(white, Math.Max(1.2, size * 0.085))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        switch (_platform)
        {
            case "YOUTUBE":
            {
                var play = new StreamGeometry();
                using var ctx = play.Open();
                ctx.BeginFigure(P(.37, .25), true, true);
                ctx.LineTo(P(.78, .50), true, false);
                ctx.LineTo(P(.37, .75), true, false);
                play.Freeze();
                dc.DrawGeometry(white, null, play);
                break;
            }
            case "TWITCH":
            {
                var bubble = new StreamGeometry();
                using (var ctx = bubble.Open())
                {
                    ctx.BeginFigure(P(.18, .18), false, true);
                    ctx.LineTo(P(.82, .18), true, false);
                    ctx.LineTo(P(.82, .66), true, false);
                    ctx.LineTo(P(.62, .84), true, false);
                    ctx.LineTo(P(.48, .84), true, false);
                    ctx.LineTo(P(.48, .72), true, false);
                    ctx.LineTo(P(.18, .72), true, false);
                }
                bubble.Freeze();
                dc.DrawGeometry(null, pen, bubble);
                dc.DrawLine(pen, P(.40, .34), P(.40, .55));
                dc.DrawLine(pen, P(.62, .34), P(.62, .55));
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
                dc.DrawRoundedRectangle(null, pen, new Rect(P(.18, .28), P(.82, .72)), size * .12, size * .12);
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